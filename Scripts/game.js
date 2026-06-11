let state;
let catalog;
let manualHuntAvailableAt;
let manualHuntUnlockTimer;
let manualHuntRequestPending = false;
let selectedCollectionAreaId = 0;
let selectedAutoHuntAreaId = 0;
let authentication;
let serverTimeOffsetMs = 0;
let collectionToastQueue = [];
let collectionToastHideTimer;
let collectionToastNextTimer;
let collectionToastShowing = false;
let selectedRankingCategory = "level";
let weaponGyroListening = false;
let weaponGyroReceived = false;
const weaponGyroPermissionKey = "enhanceAddictionWeaponMotionGranted";
const $ = selector => document.querySelector(selector);
const number = value => Number(value).toLocaleString("ko-KR");
const experience = value => Number(value).toLocaleString("ko-KR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const percent = value => `${(value * 100).toLocaleString("ko-KR", { maximumFractionDigits: 2 })}%`;
const manualHuntResultVisibleMs = 1300;
const manualHuntResultCloseMs = 220;

// 서버나 DB에 저장된 이미지 경로가 윈도우식 역슬래시여도 브라우저 URL로 사용할 수 있게 보정합니다.
function normalizeAssetPath(value) {
    return String(value || "").replace(/\\/g, "/");
}

// 몬스터 이미지가 없거나 예전 PNG 경로가 깨질 때, 항상 배포되는 webp 파일로 한 번 더 대체합니다.
function monsterImagePath(primaryPath, monsterKey) {
    const commonKey = String(monsterKey || "").replace(/^(area-\d{2})-(normal|elite|golden)-(\d{2})$/i, "$1-$3");
    const fallbackPath = commonKey ? `Content/monsters/${commonKey}.webp` : "";
    return normalizeAssetPath(primaryPath || fallbackPath);
}

// 이미지 로딩 실패 시 카드 전체를 숨기지 않고 webp 대체 파일을 먼저 시도합니다.
function useFallbackImage(image) {
    const fallbackPath = image.dataset.fallback;
    if (fallbackPath) {
        image.dataset.fallback = "";
        image.src = fallbackPath;
        return;
    }
    image.hidden = true;
}

// 데스크톱 브라우저의 로컬 시계/타이머가 서버와 어긋나도 게임 시간은 서버 기준으로 표시합니다.
function syncServerClock(nextState) {
    if (!nextState?.serverNow) return;
    serverTimeOffsetMs = new Date(nextState.serverNow).getTime() - Date.now();
}

function serverNowMs() {
    return Date.now() + serverTimeOffsetMs;
}

function SessionExpiredError(message) {
    this.name = "SessionExpiredError";
    this.message = message || "로그인 세션이 만료되었습니다. 다시 로그인해주세요.";
}
SessionExpiredError.prototype = Object.create(Error.prototype);
SessionExpiredError.prototype.constructor = SessionExpiredError;

// 세션이 끊기거나 다른 기기 로그인으로 현재 접속이 종료되면 로그인 화면으로 되돌립니다.
function redirectToLogin(message) {
    state = null;
    authentication = {
        authenticated: false,
        allowDevelopmentLogin: location.hostname === "localhost" || location.hostname === "127.0.0.1"
    };
    manualHuntRequestPending = false;
    manualHuntAvailableAt = null;
    $("#login-panel").hidden = false;
    $("#development-login").hidden = !authentication.allowDevelopmentLogin;
    $("#nickname-panel").hidden = true;
    $("#game").hidden = true;
    $("#admin-link").hidden = true;
    toast(message || "로그인 세션이 만료되었습니다. 다시 로그인해주세요.");
}

// 액션 응답은 도감 전체를 생략할 수 있으므로 기존 도감 상태를 보존합니다.
function mergeState(nextState) {
    if (!nextState) return state;
    if (state?.collection && !nextState.collection) nextState.collection = state.collection;
    if (state?.collectionEnabled !== undefined && nextState.collectionEnabled === undefined) {
        nextState.collectionEnabled = state.collectionEnabled;
    }
    return nextState;
}

// 직접 사냥 응답에 포함된 신규 도감 등록만 로컬 도감에 반영합니다.
function applyCollectionRegistrations(nextState, details) {
    const collection = nextState?.collection;
    const registrations = details?.registrations || [];
    if (!collection || !registrations.length) return;

    for (const registration of registrations) {
        const registered = registration.Registered ?? registration.registered;
        const duplicate = registration.Duplicate ?? registration.duplicate;
        const monsterKey = registration.MonsterKey || registration.monsterKey;
        if (!registered || duplicate || !monsterKey) continue;

        for (const area of collection.areas || []) {
            const monster = (area.monsters || []).find(candidate => candidate.key === monsterKey);
            if (!monster || monster.collected) continue;
            monster.collected = true;
            collection.collectedCount = Number(collection.collectedCount || 0) + 1;
            break;
        }
    }
}

function adjustedArea(areaId) {
    return (state?.manualHunt?.availableAreas || state?.availableAreas || catalog?.areas || [])
        .find(candidate => candidate.id === areaId);
}

// 서버 기능을 추가할 때는 Api/GameApi.ashx.cs의 action과 이 함수를 함께 확인하세요.
// 지정한 게임 API를 호출하고 실패 응답은 사용자용 오류로 바꿉니다.
async function api(actionName, body) {
    const response = await fetch(`Api/GameApi.ashx?action=${actionName}`, {
        method: body === undefined ? "GET" : "POST",
        headers: body === undefined ? {} : { "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
    const result = await response.json().catch(() => ({}));
    if (response.status === 401) throw new SessionExpiredError(result.message);
    if (!response.ok) throw new Error(result.message || "요청을 처리하지 못했습니다.");
    return result;
}

// 로그인 상태, 게임 규칙, 사용자 상태를 순서대로 받아 첫 화면을 구성합니다.
async function load() {
    authentication = await api("auth");
    $("#login-panel").hidden = authentication.authenticated;
    $("#development-login").hidden = !authentication.allowDevelopmentLogin;
    if (!authentication.authenticated) return;
    if (authentication.isBanned) throw new Error(authentication.banMessage || "접속이 제한된 계정입니다.");
    catalog = await api("catalog");
    state = await api("state");
    syncServerClock(state);
    $("#admin-link").hidden = !authentication.isOperator;
    $("#collection-tab").hidden = !state.collectionEnabled;
    $("#rift-tab").hidden = !state.rift?.enabled;
    $("#collection-guide").hidden = !state.collectionEnabled;
    renderRates();
    renderGuide();
    showPlayerOrNickname();
    render();
}

// 게임 행동을 서버에 요청하고 최신 상태와 결과 알림을 화면에 반영합니다.
async function action(name, body = {}) {
    const isManualHunt = name === "hunt-manual";
    try {
        // 서버 응답을 기다리는 동안 중복 클릭을 막습니다.
        if (isManualHunt) {
            manualHuntRequestPending = true;
            manualHuntAvailableAt = new Date(serverNowMs() + 1000).toISOString();
            scheduleManualHuntButtonUpdate();
            updateManualHuntButton();
        }

        const result = await api(name, body);

        const details = result.Details || result.details;
        const nextState = mergeState(result.State || result.state);
        if (isManualHunt) applyCollectionRegistrations(nextState, details);
        state = nextState;
        syncServerClock(state);
        manualHuntAvailableAt = state.manualHunt.availableAt;
        if (isManualHunt) {
            manualHuntRequestPending = false;
            scheduleManualHuntButtonUpdate();
        }
        updateManualHuntButton();
        render();
        toast(result.Message || result.message);
        if (isManualHunt) {
            showManualHuntResult(details);
            showCollectionRegistrations(details);
        }
    } catch (error) {
        if (error instanceof SessionExpiredError) {
            redirectToLogin(error.message);
            return;
        }
        if (isManualHunt) {
            manualHuntRequestPending = false;
            manualHuntAvailableAt = state?.manualHunt?.availableAt;
            scheduleManualHuntButtonUpdate();
        }
        toast(error.message);
        if (state) render();
    }
}

// 닉네임 설정 여부에 따라 최초 설정 화면과 게임 화면을 전환합니다.
function showPlayerOrNickname() {
    const hasNickname = Boolean(state.nickname);
    $("#nickname-panel").hidden = hasNickname;
    $("#game").hidden = !hasNickname;
}

// 사용자 상태를 공통 상단 정보와 각 게임 패널에 반영합니다.
function render() {
    if (!state) return;
    $("#player-name").innerHTML = state.nickname
        ? nicknameProfileHtml(`${state.nickname} 님의 대장간`, state.profileMonster?.monsterKey, state)
        : "";
    renderHotTimeBanner();
    $("#gold").textContent = number(state.gold);
    $("#weapon").textContent = `+${state.weaponLevel} ${state.weaponName || "검"}`;
    renderWeaponImage();
    $("#attack").textContent = number(state.attackPower);
    $("#tickets").textContent = `${state.protectionTickets}장`;
    $("#weapon-orb").textContent = `+${state.weaponLevel}`;
    $("#level").textContent = `Lv. ${state.level}`;
    $("#exp-text").textContent = state.requiredExperience
        ? `${experience(state.experience)} / ${experience(state.requiredExperience)} EXP`
        : "MAX LEVEL";
    $("#exp-fill").style.width = state.requiredExperience
        ? `${Math.min(100, state.experience / state.requiredExperience * 100)}%`
        : "100%";
    $("#stat-points").textContent = `사용 가능한 스탯 포인트 ${state.availableStatPoints}`;
    renderHunt();
    renderEnhance();
    renderBoss();
    renderRift();
    renderStats();
    renderCollection();
    $("#messages").innerHTML = state.recentMessages
        .map(message => `<li>${formatLogMessage(message)}</li>`)
        .join("");
}

function safeMonsterKey(value) {
    const key = String(value || "");
    return /^area-\d{2}-(normal|elite|golden)-\d{2}$/i.test(key) ? key : "";
}

function profileGradeFromKey(monsterKey) {
    const key = safeMonsterKey(monsterKey);
    const match = key.match(/^area-\d{2}-(normal|elite|golden)-\d{2}$/i);
    return match ? match[1].toLowerCase() : "normal";
}

function profileBadgeHtml(monsterKey) {
    const key = safeMonsterKey(monsterKey);
    if (!key) return "";
    const monster = findCollectionMonster(key) || { key, grade: profileGradeFromKey(key), name: "프로필 몬스터", imagePath: "" };
    const imagePath = monsterImagePath(monster.imagePath, key);
    const fallbackPath = monsterImagePath("", key);
    return `
        <button class="profile-badge profile-badge-${monster.grade}" type="button" title="프로필 크게 보기" onclick="openProfileMonsterModal('${key}')">
            <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
        </button>`;
}

function nicknameProfileHtml(nickname, monsterKey, effects = {}) {
    const colorClass = nicknameColorClass(effects.nicknameColor);
    const glowClass = rankGlowClass(effects.riftRankGlow || effects.rankGlow);
    const badge = effects.riftRankBadge || effects.rankBadge || "";
    const title = titleDisplayName(effects.title || effects.titleKey);
    return `
        <span class="nickname-with-profile ${glowClass}">
            ${profileBadgeHtml(monsterKey)}
            <span class="nickname-text ${colorClass}">
                ${title ? `<small class="nickname-title">${escapeHtml(title)}</small>` : ""}
                <span>${escapeHtml(nickname)}</span>
            </span>
            ${badge ? `<span class="rift-rank-badge">${escapeHtml(badge)}</span>` : ""}
        </span>`;
}

function titleDisplayName(keyOrName) {
    const value = String(keyOrName || "");
    const titles = {
        "title-rift-ruler": "균열의 지배자",
        "title-rift-conqueror": "균열 정복자",
        "title-rift-chaser": "균열 추격자",
        "title-rift-challenger": "균열 도전자",
        "title-challenger": "차원의 도전자",
        "title-survivor": "뒤틀림을 견딘 자",
        "title-stalker": "심연의 추적자",
        "title-cleaver": "균열을 가른 자"
    };
    return titles[value] || value;
}

function nicknameColorClass(key) {
    const normalized = String(key || "").replace(/^color-/, "");
    return ["blue", "purple", "red", "gold", "rainbow"].includes(normalized)
        ? `nickname-color-${normalized}`
        : "";
}

function rankGlowClass(key) {
    const normalized = String(key || "").toLowerCase();
    return ["gold", "silver", "bronze"].includes(normalized)
        ? `rift-rank-glow-${normalized}`
        : "";
}
function formatLogMessage(message) {
    return escapeHtml(message).replace(/\[(일반|정예|황금)\]/g, (match, gradeName) => {
        const grade = gradeName === "황금" ? "golden" : gradeName === "정예" ? "elite" : "normal";
        return `<span class="log-grade log-grade-${grade}">[${gradeName}]</span>`;
    });
}

function renderWeaponImage() {
    const display = $("#weapon-display");
    const imagePath = normalizeAssetPath(state.weaponImagePath);

    if (!imagePath) {
        display.hidden = true;
        return;
    }

    const tier = weaponTierForLevel(state.weaponLevel);
    display.innerHTML = [
        weaponShowcaseHtml(imagePath, state.weaponName || "검", tier),
        `<button class="weapon-motion-button" id="weapon-motion-button" type="button" hidden>모션 효과 켜기</button>`,
        localWeaponPreviewHtml(imagePath)
    ].join("");
    bindWeaponTiltEffects(display);
    display.hidden = false;
}

function renderHotTimeBanner() {
    const banner = $("#hot-time-banner");
    const hot = state.hotTime;
    if (!banner || !hot || !hot.enabled || !hot.active) {
        if (banner) banner.hidden = true;
        return;
    }

    banner.innerHTML = `
        <div>
            <strong>핫타임 이벤트 진행 중</strong>
            <p>${escapeHtml(hot.startsAtKst)} ~ ${escapeHtml(hot.endsAtKst)}</p>
        </div>
        <div class="hot-time-rewards">
            <span>골드 ${number(hot.goldMultiplier)}배</span>
            <span>경험치 ${number(hot.experienceMultiplier)}배</span>
        </div>`;
    banner.hidden = false;
}

function weaponTierForLevel(level) {
    if (level >= 25) return "rainbow";
    if (level >= 20) return "gold";
    if (level >= 15) return "purple";
    if (level >= 10) return "blue";
    return "silver";
}

function weaponShowcaseHtml(imagePath, weaponName, tier) {
    return `
        <div class="weapon-showcase">
          <div class="weapon-frame weapon-tier-${tier}">
            <div class="weapon-holo"></div>
            <img src="${escapeHtml(imagePath)}" alt="${escapeHtml(weaponName)}" onerror="this.hidden=true" />
          </div>
        </div>`;
}

function bindWeaponTiltEffects(root) {
    bindWeaponGyroEffects(root);
    root.querySelectorAll(".weapon-showcase").forEach(container => {
        const overlay = container.querySelector(".weapon-holo");
        const frame = container.querySelector(".weapon-frame");
        if (!overlay || !frame) return;

        container.addEventListener("mousemove", event => {
            const rect = container.getBoundingClientRect();
            const x = event.clientX - rect.left;
            const y = event.clientY - rect.top;
            const rotateY = (x / rect.width - .5) * 18;
            const rotateX = (y / rect.height - .5) * -18;
            const light = Math.min(.9, Math.max(.18, x / rect.width));

            applyWeaponTilt(container, rotateX, rotateY, light, x / rect.width * 100 + y / rect.height * 24);
        });

        container.addEventListener("mouseleave", () => {
            resetWeaponTilt(container);
        });
    });
}

function bindWeaponGyroEffects(root) {
    if (!window.DeviceOrientationEvent || !window.matchMedia("(hover: none)").matches) return;

    const button = $("#weapon-motion-button");
    if (typeof DeviceOrientationEvent.requestPermission !== "function") {
        if (button) button.hidden = true;
        startWeaponGyroListening();
        return;
    }

    if (button) {
        button.hidden = localStorage.getItem(weaponGyroPermissionKey) === "granted";
        button.textContent = "모션 효과 켜기";
    }

    if (localStorage.getItem(weaponGyroPermissionKey) === "granted") {
        startWeaponGyroListening();
        setTimeout(() => {
            if (!weaponGyroReceived && button) {
                button.hidden = false;
                button.textContent = "모션 효과 켜기";
            }
        }, 1600);
    }

    if (!button || button.dataset.motionBound === "true") return;
    button.dataset.motionBound = "true";
    button.addEventListener("click", async () => {
        button.disabled = true;
        button.textContent = "권한 확인 중...";
        try {
            const permission = await DeviceOrientationEvent.requestPermission();
            if (permission === "granted") {
                localStorage.setItem(weaponGyroPermissionKey, "granted");
                button.hidden = true;
                startWeaponGyroListening();
                return;
            }

            localStorage.removeItem(weaponGyroPermissionKey);
            button.textContent = "모션 권한이 필요합니다";
        } catch {
            localStorage.removeItem(weaponGyroPermissionKey);
            button.textContent = "모션 효과 켜기";
        } finally {
            button.disabled = false;
        }
    });
}

function startWeaponGyroListening() {
    if (weaponGyroListening) return;
    weaponGyroListening = true;
    let baseline = null;

    const listen = () => window.addEventListener("deviceorientation", event => {
        if (typeof event.beta !== "number" || typeof event.gamma !== "number") return;
        weaponGyroReceived = true;
        const button = $("#weapon-motion-button");
        if (button) button.hidden = true;
        if (!baseline) {
            baseline = { beta: event.beta, gamma: event.gamma };
            document.querySelectorAll("#weapon-display .weapon-showcase").forEach(resetWeaponTilt);
            return;
        }

        const deltaGamma = normalizeAngleDelta(event.gamma - baseline.gamma);
        const deltaBeta = normalizeAngleDelta(event.beta - baseline.beta);
        const rotateY = clamp(deltaGamma, -24, 24) * .55;
        const rotateX = clamp(deltaBeta, -24, 24) * -.42;
        const position = 50 + rotateY * 2 + rotateX;
        const opacity = Math.min(.85, Math.max(.18, (Math.abs(rotateY) + Math.abs(rotateX)) / 28 + .18));
        document.querySelectorAll("#weapon-display .weapon-showcase").forEach(container =>
            applyWeaponTilt(container, rotateX, rotateY, opacity, position));
    }, true);

    listen();
}

function normalizeAngleDelta(value) {
    if (value > 180) return value - 360;
    if (value < -180) return value + 360;
    return value;
}

function applyWeaponTilt(container, rotateX, rotateY, opacity, backgroundPosition) {
    const frame = container.querySelector(".weapon-frame");
    const overlay = container.querySelector(".weapon-holo");
    if (!frame || !overlay) return;
    frame.style.transform = `perspective(420px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
    overlay.style.backgroundPosition = `${backgroundPosition}%`;
    overlay.style.opacity = opacity;
}

function resetWeaponTilt(container) {
    const frame = container.querySelector(".weapon-frame");
    const overlay = container.querySelector(".weapon-holo");
    if (!frame || !overlay) return;
    frame.style.transform = "perspective(420px) rotateX(0deg) rotateY(0deg)";
    overlay.style.opacity = "0";
    overlay.style.backgroundPosition = "100%";
}

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

function localWeaponPreviewHtml(imagePath) {
    if (!["localhost", "127.0.0.1"].includes(location.hostname)) return "";

    const previews = [
        ["silver", "은빛 반사"],
        ["blue", "파란 오라"],
        ["purple", "보라 파동"],
        ["gold", "금빛 반짝"],
        ["rainbow", "붉은 무지개"]
    ];
    return `
        <div class="weapon-preview-row">
          ${previews.map(([tier, label]) =>
              weaponShowcaseHtml(imagePath, label, tier)).join("")}
        </div>`;
}

// 자동 사냥, 직접 사냥, 입장 가능한 사냥터 목록을 표시합니다.
function renderHunt() {
    $("#hunt-running").innerHTML = state.hunt
        ?
        `<div class="running-card">
            <div>
                <strong>${state.hunt.areaName} 자동 사냥 중</strong>
                <div id="hunt-timer" class="hunt-timer"></div>
                <div id="hunt-rewards" class="hunt-rewards"></div>
                <div id="hunt-remaining" class="hunt-remaining"></div>
            </div>
            <button onclick="action('hunt-claim')">종료 및 정산</button>
        </div>`
        : "";
    updateAutomaticHuntBudget();
    updateTimer();
    const selectedAutoAreaId = Number($("#auto-hunt-area").value || selectedAutoHuntAreaId || state.manualHunt.areaId);
    $("#auto-hunt-area").innerHTML = state.manualHunt.availableAreas
        .filter(area => area.canEnter)
        .map(area => `<option value="${area.id}" ${area.id === selectedAutoAreaId ? "selected" : ""}>${area.name}</option>`)
        .join("");
    selectedAutoHuntAreaId = Number($("#auto-hunt-area").value || state.manualHunt.areaId);
    updateAutoHuntDetails();
    const selectedAreaId = Number($("#manual-hunt-area").value || state.manualHunt.areaId);
    $("#manual-hunt-area").innerHTML = state.manualHunt.availableAreas
        .filter(area => area.canEnter)
        .map(area => `<option value="${area.id}" ${area.id === selectedAreaId ? "selected" : ""}>${area.name}</option>`)
        .join("");
    updateManualHuntDetails();
    updateManualHuntButton();
    $("#areas").innerHTML = state.availableAreas
        .map(area => `
            <article class="area-card">
                <h3>${area.name}</h3>
                <p>
                    시간당 ${number(area.goldPerHour)} 골드 ·
                    경험치 ${number(area.experiencePerHour)} ·
                    입장 +${area.requiredEnhancement}
                </p>
                <button
                    ${!area.canEnter || state.hunt ? "disabled" : ""}
                    onclick="action('hunt-start', { areaId: ${area.id} })">
                    사냥 시작
                </button>
            </article>`)
        .join("");
}

// 직접 사냥터 선택에 맞춰 시간당 기대 보상을 갱신합니다.
// 자동 사냥 선택값에 맞춰 시간당 보상과 입장 조건을 보여줍니다.
function updateAutoHuntDetails() {
    const areaId = Number($("#auto-hunt-area").value || selectedAutoHuntAreaId || state.manualHunt.areaId);
    const area = adjustedArea(areaId);
    if (!area) return;

    selectedAutoHuntAreaId = area.id;
    $("#auto-hunt-details").textContent =
        `${area.name} 기준 · 시간당 ${number(area.goldPerHour)} 골드 · `
        + `경험치 ${number(area.experiencePerHour)} · 입장 +${area.requiredEnhancement}`;
    $("#auto-hunt-button").disabled = Boolean(state.hunt) || !area.canEnter;
}

function updateManualHuntDetails() {
    const areaId = Number($("#manual-hunt-area").value || state.manualHunt.areaId);
    const area = adjustedArea(areaId);
    if (!area) return;

    $("#manual-hunt-details").textContent =
        `${area.name} 기준 · 평균 시간당 `
        + `${number(area.manualGoldPerHour ?? area.goldPerHour * 1.5)} 골드 · 경험치 `
        + `${experience(area.manualExperiencePerHour ?? area.experiencePerHour * 1.25)}`;
}

// 현재 강화 비용과 성공·유지·파괴 확률을 표시합니다.
function renderEnhance() {
    const rule = state.currentEnhancement;
    if (!rule) {
        $("#enhance-details").innerHTML = "<strong>최고 강화 단계입니다.</strong>";
        $("#enhance-button").disabled = true;
        return;
    }
    const base = catalog.enhancements[rule.currentLevel];
    const baseRateDescription = base.successRate !== rule.successRate
        ? `<small>장인의 손길 적용 전 성공률 ${percent(base.successRate)}</small>`
        : "";
    $("#enhance-details").innerHTML = `
        <strong>+${rule.currentLevel} → +${rule.currentLevel + 1}</strong>
        <p>비용 ${number(rule.cost)} 골드</p>
        <p>
            성공 ${percent(rule.successRate)} ·
            유지 ${percent(rule.keepRate)} ·
            파괴 ${percent(rule.destroyRate)}
        </p>
        ${baseRateDescription}`;
    $("#enhance-button").disabled = Boolean(state.hunt) || state.gold < rule.cost;
    $("#use-ticket").disabled = rule.destroyRate === 0;
}

// 다음 관문 보스와 도전 가능 여부를 표시합니다.
function renderBoss() {
    const boss = state.nextBoss;
    $("#boss-card").innerHTML = !boss
        ? `
            <h3>모든 관문 보스를 처치했습니다.</h3>
            <p>최고 강화를 향해 나아가세요.</p>`
        : `
            <h3>${boss.name}</h3>
            <p>체력 <strong>${number(boss.health)}</strong></p>
            <p>도전 조건 <strong>무기 +${boss.requiredEnhancement}</strong></p>
            <button ${!boss.canChallenge || state.hunt ? "disabled" : ""} onclick="action('boss')">
                보스 도전
            </button>`;
}

// 보유 스탯과 투자 가능한 포인트를 표시합니다.
function renderStats() {
    $("#available-points").textContent = `남은 포인트 ${state.availableStatPoints}`;
    const rows = [
        ["dualWield", "이도류", state.stats.dualWield, "직접 사냥 추가 토벌 확률", .5],
        ["goldGain", "노련한 사냥꾼", state.stats.goldGain, "모든 사냥 골드 획득량", 1],
        ["experienceGain", "성장의 축복", state.stats.experienceGain, "모든 사냥 경험치 획득량", 1],
        ["artisanTouch", "장인의 손길", state.stats.artisanTouch, "강화 성공률 상대 보정", .5]
    ];
    $("#stats-list").innerHTML = rows
        .map(([key, name, value, description, perPoint]) => `
            <article class="stat-card">
                <div>
                    <h3>${name} <small>Lv. ${value}/20</small></h3>
                    <p>${description} +${value * perPoint}%</p>
                </div>
                <button
                    ${state.availableStatPoints <= 0 || value >= 20 ? "disabled" : ""}
                    onclick="action('stats-invest', { stat: '${key}' })">
                    +1
                </button>
            </article>`)
        .join("");
    $("#reset-stats").textContent = `스탯 초기화 · ${number(state.statResetCost)} 골드`;
}

function renderRift() {
    const rift = state.rift;
    const tab = $("#rift-tab");
    if (tab) tab.hidden = !rift?.enabled;
    if (!rift || !rift.enabled) return;

    const statusText = rift.active ? "진행 중" : rift.settling ? "정산 중" : "대기 중";
    $("#rift-period").textContent = `${rift.startsAtKst} ~ ${rift.endsAtKst} · ${statusText}`;
    const imagePath = monsterImagePath(rift.bossImagePath, `area-${String(rift.bossAreaId).padStart(2, "0")}-normal-10`);
    $("#rift-hero").innerHTML = `
        <div class="rift-boss-card">
            <img src="${escapeHtml(imagePath)}" alt="" onerror="useFallbackImage(this)" />
            <div>
                <strong>${escapeHtml(rift.bossName)}</strong>
                <p>${escapeHtml(rift.seasonName)}</p>
                <span>보정 전투력 ${number(rift.combatPower)} · 1회 피해량 ${number(Math.floor(rift.combatPower * .85))}~${number(Math.ceil(rift.combatPower * 1.15))}</span>
            </div>
        </div>`;
    $("#rift-stats").innerHTML = [
        ["보유 타격권", `${number(rift.tickets)}개`],
        ["오늘 획득", `${number(rift.dailyTicketsEarned)} / ${number(rift.dailyTicketLimit)}`],
        ["다음 타격권", `${number(Math.max(0, rift.huntsPerTicket - rift.nextTicketProgress))}회`],
        ["누적 피해", number(rift.damage)],
        ["주간 직접사냥", number(rift.weeklyManualHuntCount)],
        ["균열 파편", number(rift.coins)]
    ].map(item => `<article><span>${item[0]}</span><strong>${item[1]}</strong></article>`).join("");
    $("#rift-reward-list").innerHTML = (rift.rewardRules || []).map(rule => `
        <article>
            <strong>${escapeHtml(rule.rank)}</strong>
            <span>${escapeHtml(rule.reward)}</span>
        </article>`).join("");
    $("#rift-hit-button").disabled = !rift.active || rift.tickets <= 0;
    $("#rift-hit-button").textContent = rift.active ? "균열 타격" : rift.settling ? "정산 중" : "타격 대기";
    renderRiftShop();
}

function renderRiftShop() {
    const rift = state.rift;
    const shop = $("#rift-shop");
    if (!rift?.shopEnabled) {
        shop.hidden = true;
        return;
    }

    $("#rift-coins").textContent = `보유 ${number(rift.coins)} 균열 파편`;
    $("#rift-shop-list").innerHTML = (rift.shopItems || []).map(item => `
        <article class="rift-shop-item">
            <div>
                <strong>${escapeHtml(item.name)}</strong>
                <span>${item.type === "color" ? "닉네임 색상 7일" : "영구 칭호"}</span>
            </div>
            <button onclick="buyRiftItem('${escapeHtml(item.key)}')" ${rift.coins < item.cost ? "disabled" : ""}>
                ${number(item.cost)}개
            </button>
        </article>`).join("");
    shop.hidden = false;
}

async function buyRiftItem(itemKey) {
    await action("rift-buy", { itemKey });
}

// 서버에서 실시간 랭킹을 받아 랭킹 표를 갱신합니다.
async function loadRankings(category = selectedRankingCategory) {
    selectedRankingCategory = category;
    document.querySelectorAll(".ranking-tab").forEach(tab => {
        tab.classList.toggle("active", tab.dataset.ranking === selectedRankingCategory);
    });

    const ranking = await api(`rankings&category=${encodeURIComponent(selectedRankingCategory)}`);
    const rows = ranking.rows || ranking;
    const metric = rankingMetric(selectedRankingCategory);
    $("#ranking-head").innerHTML = `<th>순위</th><th>닉네임</th><th>${metric.label}</th>`;
    $("#ranking-body").innerHTML = rows
        .map(row => `
            <tr>
                <td>${row.rank}</td>
                <td class="nickname-cell">${nicknameProfileHtml(row.nickname, row.profileMonsterKey, row)}</td>
                <td>${metric.value(row)}</td>
            </tr>`)
        .join("");
}

function rankingMetric(category) {
    if (category === "enhancement") {
        return { label: "최대강화", value: row => `+${number(row.highestWeaponLevel || 0)}` };
    }
    if (category === "collection") {
        return { label: "도감등록", value: row => `${number(row.collectionCount || 0)}종` };
    }
    if (category === "manualhunt") {
        return { label: "직접사냥", value: row => `${number(row.manualHuntCount || 0)}회` };
    }
    if (category === "rift") {
        return { label: "주간균열", value: row => number(row.riftDamage || 0) };
    }
    return { label: "레벨", value: row => `Lv. ${number(row.level || 0)}` };
}

// 전체 강화 단계의 기본 확률표를 표시합니다.
function renderRates() {
    $("#rates-body").innerHTML = catalog.enhancements
        .map(rule => `
            <tr>
                <td>+${rule.currentLevel} → +${rule.currentLevel + 1}</td>
                <td>${number(rule.cost)}</td>
                <td>${percent(rule.successRate)}</td>
                <td>${percent(rule.keepRate)}</td>
                <td>${percent(rule.destroyRate)}</td>
            </tr>`)
        .join("");
}

// 사냥터별 입장 조건과 시간당 보상을 게임 안내 표에 표시합니다.
function renderGuide() {
    $("#guide-areas").innerHTML = catalog.areas
        .map(area => `
            <tr>
                <td>${area.name}</td>
                <td>+${area.requiredEnhancement}</td>
                <td>${number(area.goldPerHour)}</td>
                <td>${number(area.experiencePerHour)}</td>
                <td>${area.bossRequiredEnhancement === null ? "최종 지역" : `+${area.bossRequiredEnhancement}`}</td>
            </tr>`)
        .join("");
    if (!state.collectionEnabled) return;
    const monsters = catalog.monsters;
    $("#guide-monster-rates").innerHTML = [
        ["일반", monsters.normalRate, monsters.collectionRates.normal],
        ["정예", monsters.eliteRate, monsters.collectionRates.elite],
        ["황금", monsters.goldenRate, monsters.collectionRates.golden]
    ].map(([grade, appearanceRate, collectionRate]) => `
        <tr>
            <td>${grade}</td>
            <td>${percent(appearanceRate)}</td>
            <td>${percent(collectionRate)}</td>
        </tr>`)
        .join("");
}

// 사냥터별 하위 탭과 등록 여부를 포함한 도감 카드를 표시합니다.
function renderCollection() {
    if (!state.collection) return;
    const areas = state.collection.areas;
    const selectedArea = areas.find(area => area.id === selectedCollectionAreaId) || areas[0];
    selectedCollectionAreaId = selectedArea.id;
    $("#collection-progress").textContent = `${state.collection.collectedCount} / ${state.collection.totalCount}`;
    $("#collection-area-tabs").innerHTML = areas
        .map(area => `
            <button
                class="collection-area-tab ${area.id === selectedArea.id ? "active" : ""}"
                onclick="selectCollectionArea(${area.id})">
                ${area.name}
            </button>`)
        .join("");
    $("#collection-grid").innerHTML = selectedArea.monsters
        .map(monster => {
            const gradeName = monster.grade === "golden" ? "황금" : monster.grade === "elite" ? "정예" : "일반";
            const imagePath = monsterImagePath(monster.imagePath, monster.key);
            const fallbackPath = monsterImagePath("", monster.key);
            const cardClasses = [
                "collection-card",
                monster.collected ? "collected" : "locked"
            ].join(" ");
            const clickAction = monster.collected
                ? `onclick="openCollectionModal('${escapeHtml(monster.key)}')"`
                : "";
            return `
                <article class="${cardClasses}" ${clickAction}>
                    <div class="collection-image">
                        <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
                        <span>${monster.collected ? "등록 완료" : "미등록"}</span>
                    </div>
                    <div class="collection-card-info">
                        <strong>${monster.collected ? escapeHtml(monster.name) : "???"}</strong>
                        <span class="collection-grade-${monster.grade}">${gradeName}</span>
                    </div>
                </article>`;
        })
        .join("");
}

// 도감에서 선택한 사냥터의 30개 카드만 표시합니다.
function selectCollectionArea(areaId) {
    selectedCollectionAreaId = areaId;
    renderCollection();
}

// 수집 완료된 도감 카드를 눌렀을 때 큰 정보카드를 띄웁니다.
function openCollectionModal(monsterKey) {
    const monster = findCollectionMonster(monsterKey);
    if (!monster || !monster.collected) return;

    const gradeName = monster.grade === "golden" ? "황금" : monster.grade === "elite" ? "정예" : "일반";
    const imagePath = monsterImagePath(monster.imagePath, monster.key);
    const fallbackPath = monsterImagePath("", monster.key);
    const modal = $("#collection-modal");
    modal.className = `collection-modal collection-modal-${monster.grade}`;
    modal.innerHTML = `
        <div class="collection-modal-backdrop" onclick="closeCollectionModal()"></div>
        <article class="collection-modal-card">
            <span class="collection-grade-${monster.grade}">${gradeName}</span>
            <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
            <h3>${escapeHtml(monster.name)}</h3>
            <div class="collection-modal-actions">
                <button class="profile-set-button" type="button" onclick="setProfileMonster('${monster.key}')" ${state.profileMonster?.monsterKey === monster.key ? "disabled" : ""}>
                    ${state.profileMonster?.monsterKey === monster.key ? "프로필 지정됨" : "프로필로 지정"}
                </button>
            </div>
            <p>${escapeHtml(monster.description || "아직 설명이 등록되지 않은 몬스터입니다.")}</p>
        </article>`;
    modal.hidden = false;
    requestAnimationFrame(() => modal.classList.add("show"));
}

// 도감 정보카드를 닫습니다.
function openProfileMonsterModal(monsterKey) {
    const key = safeMonsterKey(monsterKey);
    if (!key) return;
    const monster = findCollectionMonster(key) || {
        key,
        grade: profileGradeFromKey(key),
        name: "프로필 몬스터",
        description: "다른 유저가 지정한 프로필 몬스터입니다.",
        imagePath: ""
    };
    const gradeName = monster.grade === "golden" ? "황금" : monster.grade === "elite" ? "정예" : "일반";
    const imagePath = monsterImagePath(monster.imagePath, key);
    const fallbackPath = monsterImagePath("", key);
    const modal = $("#collection-modal");
    modal.className = `collection-modal collection-modal-${monster.grade}`;
    modal.innerHTML = `
        <div class="collection-modal-backdrop" onclick="closeCollectionModal()"></div>
        <article class="collection-modal-card">
            <span class="collection-grade-${monster.grade}">${gradeName} 프로필</span>
            <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
            <h3>${escapeHtml(monster.name)}</h3>
            <p>${escapeHtml(monster.description || "프로필 몬스터입니다.")}</p>
        </article>`;
    modal.hidden = false;
    requestAnimationFrame(() => modal.classList.add("show"));
}

async function setProfileMonster(monsterKey) {
    try {
        const result = await api("profile-monster", { monsterKey });
        state = mergeState(result.State || result.state);
        syncServerClock(state);
        render();
        toast(result.Message || result.message);
        openCollectionModal(monsterKey);
    } catch (error) {
        if (error instanceof SessionExpiredError) {
            redirectToLogin(error.message);
            return;
        }
        toast(error.message);
    }
}
function closeCollectionModal() {
    const modal = $("#collection-modal");
    modal.classList.remove("show");
    setTimeout(() => {
        if (!modal.classList.contains("show")) modal.hidden = true;
    }, 180);
}

// 도감 전체 목록에서 키가 같은 몬스터를 찾습니다.
function findCollectionMonster(monsterKey) {
    if (!state?.collection) return null;
    for (const area of state.collection.areas) {
        const monster = area.monsters.find(candidate => candidate.key === monsterKey);
        if (monster) return monster;
    }
    return null;
}

// 진행 중인 자동 사냥의 경과 시간과 예상 누적 보상을 갱신합니다.
function updateTimer() {
    if (!state?.hunt || !$("#hunt-timer")) return;

    const startedAt = new Date(state.hunt.startedAt).getTime();
    const rewardCapAt = new Date(state.hunt.rewardCapAt).getTime();

    // 서버 위치(유럽)로 인한 지연을 고려하여 현재 시간을 가져옴
    const now = serverNowMs();

    // [보정 로직] 
    // 만약 서버에서 온 시작 시간이 네트워크 지연으로 인해 현재 시간보다 아주 약간 미래라면
    // 사용자에게는 즉시 시간이 흐르는 것처럼 보이도록 0이 아닌 최소값을 보장할 수 있습니다.
    let elapsedMs = now - startedAt;

    // 음수 방지 및 최대 제한
    elapsedMs = Math.min(Math.max(0, elapsedMs), rewardCapAt - startedAt);
    const remainingMs = Math.max(0, rewardCapAt - now);

    const elapsedHours = elapsedMs / (1000 * 60 * 60);
    const area = adjustedArea(state.hunt.areaId) || catalog.areas.find(a => a.id === state.hunt.areaId);
    const currentGold = Math.floor((area.autoGoldPerHour ?? area.goldPerHour) * elapsedHours);
    const currentExp = Math.floor((area.autoExperiencePerHour ?? area.experiencePerHour) * elapsedHours);

    $("#hunt-timer").textContent = `${formatDuration(Math.floor(elapsedMs / 1000))} 누적`;
    $("#hunt-rewards").textContent = `획득 중: ${number(currentGold)} 골드 / ${number(currentExp)} EXP`;
    $("#hunt-remaining").textContent = remainingMs > 0
        ? `보상 누적 가능 ${formatDuration(Math.ceil(remainingMs / 1000))} 남음`
        : "자동 사냥 보상이 가득 찼습니다.";
}
// 오늘 사용할 수 있는 자동 사냥 잔여 시간을 갱신합니다.
function updateAutomaticHuntBudget() {
    if (!state) return;
    let seconds = Math.floor(state.automaticHuntBudget.remainingHours * 3600);
    if (state.hunt) {
        const startedAt = new Date(state.hunt.startedAt).getTime();
        const rewardCapAt = new Date(state.hunt.rewardCapAt).getTime();
        const elapsedSeconds = Math.floor((Math.min(serverNowMs(), rewardCapAt) - startedAt) / 1000);
        seconds = Math.max(0, seconds - elapsedSeconds);
    }
    $("#hunt-budget").textContent =
        `오늘 남은 자동 사냥 ${formatDuration(seconds)} / `
        + `총 ${formatDuration(Math.floor(state.automaticHuntBudget.limitHours * 3600))}`;
}
// 직접 사냥 버튼의 1초 재사용 대기시간을 갱신합니다.
function updateManualHuntButton() {
    if (!state) return;
    const at = manualHuntAvailableAt || state.manualHunt.availableAt;
    const remaining = at ? Math.max(0, new Date(at).getTime() - serverNowMs()) : 0;

    $("#manual-hunt-button").disabled = manualHuntRequestPending || remaining > 0;
    $("#manual-hunt-button").textContent = manualHuntRequestPending
        ? "처치 중..."
        : remaining > 0
            ? `${Math.ceil(remaining / 1000)}초 후 가능`
            : "몬스터 처치";
    $("#manual-hunt-cooldown").textContent = remaining > 0
        ? "다음 몬스터를 찾고 있습니다."
        : "1초마다 직접 처치할 수 있습니다.";
}
// 반복 타이머가 지연되더라도 직접 사냥 버튼이 쿨타임 종료 직후 풀리도록 갱신을 예약합니다.
function scheduleManualHuntButtonUpdate() {
    clearTimeout(manualHuntUnlockTimer);
    if (!manualHuntAvailableAt) return;

    const remaining = Math.max(0, new Date(manualHuntAvailableAt).getTime() - serverNowMs());
    manualHuntUnlockTimer = setTimeout(updateManualHuntButton, remaining + 20);
}
// 초 단위 시간을 사용자가 읽기 쉬운 시·분·초 문자열로 바꿉니다.
function formatDuration(seconds) {
    return `${Math.floor(seconds / 3600)}시간 `
        + `${Math.floor(seconds % 3600 / 60)}분 `
        + `${Math.max(0, seconds % 60)}초`;
}

// 서버 행동 결과를 화면 하단의 짧은 알림으로 표시합니다.
function toast(message) {
    $("#toast").textContent = message;
    $("#toast").classList.add("show");
    setTimeout(() => $("#toast").classList.remove("show"), 2600);
}

// 직접 사냥으로 실제 처치한 몬스터를 화면 중앙에 크게 보여줍니다.
function showManualHuntResult(details) {
    const hunt = details?.first || details?.First;
    if (!hunt) return;

    const grade = hunt.Grade || hunt.grade || "normal";
    const gradeName = grade === "golden" ? "황금" : grade === "elite" ? "정예" : "일반";
    const monsterName = hunt.MonsterName || hunt.monsterName || "몬스터";
    const monsterKey = hunt.MonsterKey || hunt.monsterKey;
    const imagePath = monsterImagePath(hunt.ImagePath || hunt.imagePath, monsterKey);
    const fallbackPath = monsterImagePath("", monsterKey);
    const box = $("#manual-hunt-result");

    box.className = `manual-hunt-result manual-hunt-result-${grade}`;
    box.innerHTML = `
        <div class="manual-hunt-result-card">
            <span>${gradeName} 처치</span>
            <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
            <strong>${escapeHtml(monsterName)}</strong>
        </div>`;
    box.hidden = false;
    box.classList.add("show");

    clearTimeout(showManualHuntResult.hideTimer);
    showManualHuntResult.hideTimer = setTimeout(() => {
        box.classList.remove("show");
        setTimeout(() => {
            if (!box.classList.contains("show")) box.hidden = true;
        }, manualHuntResultCloseMs);
    }, manualHuntResultVisibleMs);
}

// 직접 사냥에서 도감 판정이 성공하면 등록된 몬스터 이미지와 등급을 카드로 보여줍니다.
function showCollectionRegistrations(details) {
    const registrations = (details?.registrations || [])
        .filter(registration => (registration.Registered || registration.registered)
            && !(registration.Duplicate ?? registration.duplicate));
    if (!registrations.length) return;

    collectionToastQueue.push(...registrations);
    showNextCollectionToast();
}

// 도감 등록 알림을 하나씩 순서대로 보여줍니다. 연속 등록 시 이전 타이머가 새 알림을 지우지 않게 분리합니다.
function showNextCollectionToast() {
    if (collectionToastShowing || !collectionToastQueue.length) return;
    collectionToastShowing = true;

    const registration = collectionToastQueue.shift();
    const duplicate = registration.Duplicate ?? registration.duplicate;
    const grade = registration.Grade || registration.grade || "normal";
    const gradeName = grade === "golden" ? "황금" : grade === "elite" ? "정예" : "일반";
    const monsterName = registration.MonsterName || registration.monsterName || "도감 몬스터";
    const monsterKey = registration.MonsterKey || registration.monsterKey;
    const imagePath = monsterImagePath(registration.ImagePath || registration.imagePath, monsterKey);
    const fallbackPath = monsterImagePath("", monsterKey);
    const remainCount = collectionToastQueue.length
        ? `<p>대기 중인 도감 알림 ${collectionToastQueue.length}건</p>`
        : "";

    const box = $("#collection-toast");
    clearTimeout(collectionToastHideTimer);
    clearTimeout(collectionToastNextTimer);
    box.innerHTML = `
        <img src="${escapeHtml(imagePath)}" data-fallback="${escapeHtml(fallbackPath)}" alt="" onerror="useFallbackImage(this)" />
        <div>
            <strong>${duplicate ? "도감 중복 등록" : "도감 신규 등록!"}</strong>
            <span class="collection-grade-${grade}">${gradeName}</span>
            <p>${escapeHtml(monsterName)}</p>
            ${remainCount}
        </div>`;
    box.hidden = false;
    box.classList.add("show");
    collectionToastHideTimer = setTimeout(() => box.classList.remove("show"), manualHuntResultVisibleMs);
    collectionToastNextTimer = setTimeout(() => {
        if (!box.classList.contains("show")) box.hidden = true;
        collectionToastShowing = false;
        showNextCollectionToast();
    }, manualHuntResultVisibleMs + manualHuntResultCloseMs);
}

// 닉네임처럼 사용자 입력이 HTML로 해석되지 않도록 안전하게 변환합니다.
function escapeHtml(value) {
    const div = document.createElement("div");
    div.textContent = value;
    return div.innerHTML;
}

$("#nickname-form").addEventListener("submit", async event => {
    event.preventDefault();
    try {
        const result = await api("nickname", { nickname: $("#nickname-input").value });
        state = mergeState(result.State || result.state);
        showPlayerOrNickname();
        render();
        toast(result.Message || result.message);
    } catch (error) {
        if (error instanceof SessionExpiredError) {
            redirectToLogin(error.message);
            return;
        }
        toast(error.message);
    }
});
$("#enhance-button").addEventListener("click", () => action("enhance", { useProtection: $("#use-ticket").checked }));
$("#auto-hunt-area").addEventListener("change", updateAutoHuntDetails);
$("#auto-hunt-button").addEventListener("click", () => action("hunt-start", {
    areaId: Number($("#auto-hunt-area").value)
}));
$("#manual-hunt-area").addEventListener("change", updateManualHuntDetails);
$("#manual-hunt-button").addEventListener("click", () => action("hunt-manual", {
    areaId: Number($("#manual-hunt-area").value)
}));
$("#reset-stats").addEventListener("click", () => action("stats-reset"));
$("#rift-hit-button").addEventListener("click", () => action("rift-hit"));
document.querySelectorAll(".tab").forEach(tab => {
    tab.addEventListener("click", () => {
        document.querySelectorAll(".tab, .panel").forEach(item => item.classList.remove("active"));
        tab.classList.add("active");
        $(`#${tab.dataset.tab}-panel`).classList.add("active");
        if (tab.dataset.tab === "ranking") loadRankings();
    });
});
document.querySelectorAll(".ranking-tab").forEach(tab => {
    tab.addEventListener("click", () => loadRankings(tab.dataset.ranking));
});
setInterval(() => {
    updateTimer();
    updateAutomaticHuntBudget();
    updateManualHuntButton();
}, 250);
load().catch(error => {
    if (error instanceof SessionExpiredError) {
        redirectToLogin(error.message);
        return;
    }
    toast(error.message);
});
