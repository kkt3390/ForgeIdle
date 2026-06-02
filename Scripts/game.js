let state;
let catalog;
let manualHuntAvailableAt;
let manualHuntUnlockTimer;
let manualHuntRequestPending = false;
let authentication;
const $ = selector => document.querySelector(selector);
const number = value => Number(value).toLocaleString("ko-KR");
const percent = value => `${(value * 100).toLocaleString("ko-KR", { maximumFractionDigits: 2 })}%`;

// 서버 기능을 추가할 때는 Api/GameApi.ashx.cs의 action과 이 함수를 함께 확인하세요.
// 지정한 게임 API를 호출하고 실패 응답은 사용자용 오류로 바꿉니다.
async function api(actionName, body) {
    const response = await fetch(`Api/GameApi.ashx?action=${actionName}`, {
        method: body === undefined ? "GET" : "POST",
        headers: body === undefined ? {} : { "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.message || "요청을 처리하지 못했습니다.");
    return result;
}

// 로그인 상태, 게임 규칙, 사용자 상태를 순서대로 받아 첫 화면을 구성합니다.
async function load() {
    authentication = await api("auth");
    $("#login-panel").hidden = authentication.authenticated;
    $("#development-login").hidden = !authentication.allowDevelopmentLogin;
    if (!authentication.authenticated) return;
    catalog = await api("catalog");
    state = await api("state");
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
            manualHuntAvailableAt = new Date(Date.now() + 1000).toISOString();
            scheduleManualHuntButtonUpdate();
            updateManualHuntButton();
        }

        const result = await api(name, body);

        state = result.State || result.state;
        manualHuntAvailableAt = state.manualHunt.availableAt;
        if (isManualHunt) {
            manualHuntRequestPending = false;
            scheduleManualHuntButtonUpdate();
        }
        updateManualHuntButton();
        render();
        toast(result.Message || result.message);
    } catch (error) {
        if (isManualHunt) {
            manualHuntRequestPending = false;
            manualHuntAvailableAt = state?.manualHunt?.availableAt;
            scheduleManualHuntButtonUpdate();
        }
        toast(error.message);
        render();
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
    $("#player-name").textContent = state.nickname ? `${state.nickname} 님의 대장간` : "";
    $("#gold").textContent = number(state.gold);
    $("#weapon").textContent = `+${state.weaponLevel} 검`;
    $("#attack").textContent = number(state.attackPower);
    $("#tickets").textContent = `${state.protectionTickets}장`;
    $("#weapon-orb").textContent = `+${state.weaponLevel}`;
    $("#level").textContent = `Lv. ${state.level}`;
    $("#exp-text").textContent = state.requiredExperience
        ? `${number(state.experience)} / ${number(state.requiredExperience)} EXP`
        : "MAX LEVEL";
    $("#exp-fill").style.width = state.requiredExperience
        ? `${Math.min(100, state.experience / state.requiredExperience * 100)}%`
        : "100%";
    $("#stat-points").textContent = `사용 가능한 스탯 포인트 ${state.availableStatPoints}`;
    renderHunt();
    renderEnhance();
    renderBoss();
    renderStats();
    $("#messages").innerHTML = state.recentMessages
        .map(message => `<li>${escapeHtml(message)}</li>`)
        .join("");
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
    $("#manual-hunt-details").textContent =
        `${state.manualHunt.areaName} 기준 · 평균 시간당 `
        + `${number(state.manualHunt.automaticGoldPerHour * 1.5)} 골드 · 경험치 `
        + `${number(state.manualHunt.automaticExperiencePerHour * 1.25)}`;
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

// 서버에서 실시간 랭킹을 받아 랭킹 표를 갱신합니다.
async function loadRankings() {
    const rankings = await api("rankings");
    $("#ranking-body").innerHTML = rankings
        .map(row => `
            <tr>
                <td>${row.rank}</td>
                <td>${escapeHtml(row.nickname)}</td>
                <td>Lv. ${row.level}</td>
                <td>+${row.weaponLevel}</td>
                <td>+${row.highestWeaponLevel}</td>
            </tr>`)
        .join("");
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
}

// 진행 중인 자동 사냥의 경과 시간과 예상 누적 보상을 갱신합니다.
function updateTimer() {
    if (!state?.hunt || !$("#hunt-timer")) return;

    const startedAt = new Date(state.hunt.startedAt).getTime();
    const rewardCapAt = new Date(state.hunt.rewardCapAt).getTime();

    // 서버 위치(유럽)로 인한 지연을 고려하여 현재 시간을 가져옴
    const now = Date.now();

    // [보정 로직] 
    // 만약 서버에서 온 시작 시간이 네트워크 지연으로 인해 현재 시간보다 아주 약간 미래라면
    // 사용자에게는 즉시 시간이 흐르는 것처럼 보이도록 0이 아닌 최소값을 보장할 수 있습니다.
    let elapsedMs = now - startedAt;

    // 음수 방지 및 최대 제한
    elapsedMs = Math.min(Math.max(0, elapsedMs), rewardCapAt - startedAt);
    const remainingMs = Math.max(0, rewardCapAt - now);

    const elapsedHours = elapsedMs / (1000 * 60 * 60);
    const area = catalog.areas.find(a => a.id === state.hunt.areaId);
    const goldMultiplier = 1 + (state.stats.goldGain * 0.01);
    const expMultiplier = 1 + (state.stats.experienceGain * 0.01);

    const currentGold = Math.floor(area.goldPerHour * elapsedHours * goldMultiplier);
    const currentExp = Math.floor(area.experiencePerHour * elapsedHours * expMultiplier);

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
        const elapsedSeconds = Math.floor((Math.min(Date.now(), rewardCapAt) - startedAt) / 1000);
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
    const remaining = at ? Math.max(0, new Date(at).getTime() - Date.now()) : 0;

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

    const remaining = Math.max(0, new Date(manualHuntAvailableAt).getTime() - Date.now());
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
        state = result.State || result.state;
        showPlayerOrNickname();
        render();
        toast(result.Message || result.message);
    } catch (error) {
        toast(error.message);
    }
});
$("#enhance-button").addEventListener("click", () => action("enhance", { useProtection: $("#use-ticket").checked }));
$("#manual-hunt-button").addEventListener("click", () => action("hunt-manual"));
$("#reset-stats").addEventListener("click", () => action("stats-reset"));
document.querySelectorAll(".tab").forEach(tab => {
    tab.addEventListener("click", () => {
        document.querySelectorAll(".tab, .panel").forEach(item => item.classList.remove("active"));
        tab.classList.add("active");
        $(`#${tab.dataset.tab}-panel`).classList.add("active");
        if (tab.dataset.tab === "ranking") loadRankings();
    });
});
setInterval(() => {
    updateTimer();
    updateAutomaticHuntBudget();
    updateManualHuntButton();
}, 250);
load().catch(error => toast(error.message));
