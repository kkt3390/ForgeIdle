let adminState = null;
const logPageSize = 100;
let actionLogPage = 1;
let adminLogPage = 1;
const loadedAdminTabs = new Set();

const $ = selector => document.querySelector(selector);
const number = value => Number(value || 0).toLocaleString("ko-KR");
const percent = value => `${(Number(value || 0) * 100).toLocaleString("ko-KR", { maximumFractionDigits: 2 })}%`;
const escapeHtml = value => {
    const div = document.createElement("div");
    div.textContent = value ?? "";
    return div.innerHTML;
};

function searchText(id) {
    const element = $("#" + id);
    return element ? element.value.trim().toLowerCase() : "";
}

function matchesKeyword(row, keyword, fields) {
    if (!keyword) return true;
    return fields.some(field => String(row[field] ?? "").toLowerCase().includes(keyword));
}

function renderPagination(targetId, page, pageSize, totalRows, callbackName) {
    const target = $("#" + targetId);
    if (!target) return;
    const currentPage = Math.max(1, Number(page || 1));
    const totalPages = Math.max(1, Math.ceil(Number(totalRows || 0) / Number(pageSize || logPageSize)));
    if (totalRows <= logPageSize) {
        target.innerHTML = totalRows ? `전체 ${number(totalRows)}건` : "";
        return;
    }

    target.innerHTML = `
        <button ${currentPage <= 1 ? "disabled" : ""} onclick="${callbackName}(${currentPage - 1})">이전</button>
        <span>${number(currentPage)} / ${number(totalPages)} 페이지 · 전체 ${number(totalRows)}건</span>
        <button ${currentPage >= totalPages ? "disabled" : ""} onclick="${callbackName}(${currentPage + 1})">다음</button>`;
}

async function setActionLogPage(page) {
    await loadActionLogs(page);
}

async function setAdminLogPage(page) {
    await loadAdminLogs(page);
}

function adminAssetUrl(path) {
    const imagePath = String(path || "").trim().replace(/\\/g, "/");
    if (!imagePath) return "";
    if (/^(https?:|data:|\/)/i.test(imagePath)) return imagePath;
    return `/${imagePath.replace(/^\.\//, "")}`;
}

function assetPreview(path, label) {
    const imagePath = String(path || "").trim();
    if (!imagePath) return `<span class="admin-muted">이미지 없음</span>`;
    return `
        <div class="admin-image-cell">
          <img class="admin-thumb" src="${escapeHtml(adminAssetUrl(imagePath))}" alt="${escapeHtml(label || "이미지")}" onerror="this.hidden=true" />
          <span>${escapeHtml(imagePath)}</span>
        </div>`;
}

function shortText(value, length = 120) {
    const text = String(value || "");
    return text.length > length ? `${text.slice(0, length)}...` : text;
}

async function adminApi(action, body) {
    const response = await fetch(`/Api/AdminApi.ashx?action=${action}`, {
        method: body === undefined ? "GET" : "POST",
        headers: body === undefined ? {} : { "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.message || "관리자 요청 처리에 실패했습니다.");
    return result;
}

async function loadAdmin() {
    adminState = await adminApi("state");
    renderDashboard();
    $("#abuse-body").innerHTML = `<tr><td colspan="5">이상행동 후보를 불러오는 중입니다.</td></tr>`;
    setTimeout(() => loadAdminTab("abuse").catch(error => toast(error.message)), 0);
}

async function refreshDashboard() {
    const payload = await adminApi("state");
    adminState = { ...(adminState || {}), ...payload };
    renderDashboard();
    if (loadedAdminTabs.has("event")) renderHotTime();
}

async function loadAdminTab(tab, force = false) {
    if (!force && loadedAdminTabs.has(tab)) return;
    renderAdminTabLoading(tab);
    try {
        const payload = await adminApi(`tab-state&tab=${encodeURIComponent(tab)}`);
        adminState = { ...(adminState || {}), ...payload };
        loadedAdminTabs.add(tab);
        renderAdminTab(tab);
    } catch (error) {
        renderAdminTabError(tab, error.message);
        throw error;
    }
}

function renderAdminTab(tab) {
    if (tab === "abuse") renderSuspicious();
    if (tab === "users") renderUsers(adminState.operators || []);
    if (tab === "event") renderHotTime();
    if (tab === "rift") renderRift();
    if (tab === "enhancements") renderEnhancements();
    if (tab === "enhancement-proof") renderEnhancementProof();
    if (tab === "monsters") renderMonsters();
    if (tab === "weapons") renderWeapons();
    if (tab === "action-logs") renderActionLogs(adminState.recentActionLogs || { rows: [], totalRows: 0, page: 1, pageSize: logPageSize });
    if (tab === "logs") renderAdminLogs(adminState.recentAdminLogs || { rows: [], totalRows: 0, page: 1, pageSize: logPageSize });
}

function renderAdminTabLoading(tab) {
    renderAdminTabMessage(tab, "불러오는 중입니다.");
}

function renderAdminTabError(tab, message) {
    renderAdminTabMessage(tab, message || "불러오지 못했습니다.");
}

function renderAdminTabMessage(tab, message) {
    const escaped = escapeHtml(message);
    const tableTargets = {
        abuse: ["abuse-body", 5],
        users: ["users-body", 6],
        enhancements: ["enhancement-body", 6],
        monsters: ["monster-body", 6],
        weapons: ["weapon-body", 5],
        "action-logs": ["action-log-body", 6],
        logs: ["admin-log-body", 4]
    };
    if (tableTargets[tab]) {
        const [id, colspan] = tableTargets[tab];
        $("#" + id).innerHTML = `<tr><td colspan="${colspan}">${escaped}</td></tr>`;
        return;
    }
    if (tab === "event") {
        $("#hot-time-summary").textContent = message;
        return;
    }
    if (tab === "rift") {
        $("#rift-admin-summary").innerHTML = `<article class="admin-card"><span>주간 균열</span><strong>${escaped}</strong></article>`;
        $("#rift-ranking-body").innerHTML = `<tr><td colspan="6">${escaped}</td></tr>`;
        $("#rift-reward-preview-body").innerHTML = `<tr><td colspan="4">${escaped}</td></tr>`;
        $("#rift-result-list").innerHTML = `<p class="collection-description">${escaped}</p>`;
        return;
    }
    if (tab === "enhancement-proof") {
        $("#enhancement-proof-summary").innerHTML = `<article class="admin-card"><span>강화 입증</span><strong>${escaped}</strong></article>`;
        $("#enhancement-proof-body").innerHTML = `<tr><td colspan="6">${escaped}</td></tr>`;
    }
}

function renderRift() {
    const rift = adminState.rift;
    if (!rift) return;
    $("#rift-enabled").value = String(Boolean(rift.enabled));
    $("#rift-shop-enabled").value = String(Boolean(rift.shopEnabled));
    $("#rift-mode").value = rift.mode || "auto";
    $("#rift-season-name").value = rift.manualSeasonName || "테스트 균열";
    $("#rift-start").value = rift.manualStartsAtKst || "";
    $("#rift-end").value = rift.manualEndsAtKst || "";
    $("#rift-settle-end").value = rift.manualSettlementEndsAtKst || "";
    $("#rift-boss-area").value = rift.manualBossAreaId || 0;
    updateRiftSummary();

    const season = rift.season || {};
    $("#rift-admin-summary").innerHTML = [
        ["시즌", season.seasonName || "-"],
        ["상태", season.active ? "진행 중" : season.settling ? "정산 중" : "대기"],
        ["보스", `사냥터 ${season.bossAreaId ?? 0}`],
        ["시즌 키", season.seasonKey || "-"]
    ].map(item => `<article class="admin-card"><span>${item[0]}</span><strong>${escapeHtml(item[1])}</strong></article>`).join("");

    const rows = rift.rankings || [];
    $("#rift-ranking-body").innerHTML = rows.length
        ? rows.map(row => `
          <tr>
            <td>${row.rank}</td>
            <td>${escapeHtml(row.nickname || row.playerKey)}<br><small>${escapeHtml(row.playerKey)}</small></td>
            <td>${number(row.damage)}</td>
            <td>${number(row.tickets)}</td>
            <td>${number(row.weeklyManualHuntCount)}</td>
            <td>${escapeHtml(row.lastDamageAt || "")}</td>
          </tr>`).join("")
        : `<tr><td colspan="6">현재 시즌 균열 타격 기록이 없습니다.</td></tr>`;

    const previewRows = rift.rewardPreview || [];
    $("#rift-reward-preview-body").innerHTML = previewRows.length
        ? previewRows.map(row => `
          <tr>
            <td>${row.rank}</td>
            <td>${escapeHtml(row.nickname || row.playerKey)}<br><small>${escapeHtml(row.playerKey)}</small></td>
            <td>${number(row.damage)}</td>
            <td>${escapeHtml(row.rewardLabel || `${number(row.rewardCoins)} 파편`)}</td>
          </tr>`).join("")
        : `<tr><td colspan="4">현재 시즌 정산 대상자가 없습니다.</td></tr>`;

    const results = rift.recentResults || [];
    $("#rift-result-list").innerHTML = results.length
        ? results.map(result => `
          <details class="admin-table-wrap">
            <summary>${escapeHtml(result.seasonName)} · ${escapeHtml(result.settledAtKst)} · 참여 ${number(result.totalParticipants)}명 · 피해 ${number(result.totalDamage)}</summary>
            <table>
              <thead><tr><th>순위</th><th>유저</th><th>피해량</th><th>지급 보상</th></tr></thead>
              <tbody>${(result.topRankings || []).map(row => `
                <tr>
                  <td>${row.rank}</td>
                  <td>${escapeHtml(row.nickname || row.playerKey)}<br><small>${escapeHtml(row.playerKey)}</small></td>
                  <td>${number(row.damage)}</td>
                  <td>${escapeHtml(row.rewardLabel || `${number(row.rewardCoins)} 파편`)}</td>
                </tr>`).join("") || `<tr><td colspan="4">저장된 순위 스냅샷이 없습니다.</td></tr>`}</tbody>
            </table>
          </details>`).join("")
        : `<p class="collection-description">아직 정산된 주간 균열 시즌이 없습니다.</p>`;
}

function renderDashboard() {
    const dashboard = adminState.dashboard;
    const hot = adminState.hotTime || {};
    $("#dashboard").innerHTML = [
        ["전체 유저", number(dashboard.playerCount)],
        ["차단 유저", number(dashboard.bannedCount)],
        ["운영자", number(dashboard.operatorCount)],
        ["24시간 강화", number(dashboard.todayEnhanceCount)],
        ["24시간 행동", number(dashboard.todayActionCount)],
        ["핫타임", hot.active ? "적용 중" : "대기/꺼짐"]
    ].map(item => `<article class="admin-card"><span>${item[0]}</span><strong>${item[1]}</strong></article>`).join("");
}

function renderHotTime() {
    const hot = adminState.hotTime;
    $("#hot-enabled").value = String(Boolean(hot.enabled));
    $("#hot-gold").value = hot.goldMultiplier;
    $("#hot-exp").value = hot.experienceMultiplier;
    $("#base-gold").value = hot.baseGoldMultiplier || 1;
    $("#base-exp").value = hot.baseExperienceMultiplier || 1;
    $("#hot-start").value = hot.startsAtKst || "";
    $("#hot-end").value = hot.endsAtKst || "";
    updateHotTimeSummary();
}

function renderSuspicious() {
    const keyword = searchText("abuse-search");
    const rows = (adminState.suspiciousUsers || []).filter(row =>
        matchesKeyword(row, keyword, ["nickname", "playerKey", "actionType", "reason", "message"]));
    $("#abuse-body").innerHTML = rows.length
        ? rows.map(row => `
          <tr>
            <td>${escapeHtml(row.nickname || row.playerKey)}<br><small>${escapeHtml(row.playerKey)}</small></td>
            <td>${escapeHtml(row.actionType)}</td>
            <td><span class="admin-badge warn">${escapeHtml(row.reason)}</span><br>${escapeHtml(row.message)}</td>
            <td>${escapeHtml(row.createdAt)}</td>
            <td><button onclick="banUser('${escapeHtml(row.playerKey)}', true)">차단</button></td>
          </tr>`).join("")
        : `<tr><td colspan="5">현재 감지된 이상행동 후보가 없습니다.</td></tr>`;
}

function renderUsers(rows) {
    $("#users-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.nickname || "(닉네임 없음)")}<br><small>${escapeHtml(row.playerKey)}</small></td>
          <td>
            ${row.isOperator ? '<span class="admin-badge warn">운영자</span>' : '<span class="admin-badge">일반</span>'}
            ${row.isBanned ? '<span class="admin-badge danger">차단</span>' : ""}
          </td>
          <td>Lv. ${row.level}</td>
          <td>${number(row.gold)}</td>
          <td>+${row.weaponLevel}</td>
          <td>
            <button onclick="setOperator('${escapeHtml(row.playerKey)}', ${!row.isOperator})">${row.isOperator ? "운영자 해제" : "운영자 지정"}</button>
            <button onclick="banUser('${escapeHtml(row.playerKey)}', ${!row.isBanned})">${row.isBanned ? "차단 해제" : "차단"}</button>
          </td>
        </tr>`).join("");
}

function renderMonsters() {
    const keyword = searchText("monster-search");
    const rows = (adminState.monsterCatalog || []).filter(row =>
        matchesKeyword(row, keyword, ["monsterKey", "grade", "name", "imagePath", "description", "sortOrder"]));
    $("#monster-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.monsterKey)}</td>
          <td>${row.areaId} / ${escapeHtml(row.grade)} / ${row.slotNumber}</td>
          <td>${number(row.sortOrder)}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${assetPreview(row.imagePath, row.name)}</td>
          <td>
            <button onclick='editMonster(${JSON.stringify(row)})'>편집</button>
            <button onclick='deleteMonster(${JSON.stringify(row)})'>삭제</button>
          </td>
        </tr>`).join("");
}
function renderWeapons() {
    const keyword = searchText("weapon-search");
    const rows = (adminState.weaponCatalog || []).filter(row =>
        matchesKeyword(row, keyword, ["weaponKey", "name", "imagePath", "description", "sortOrder"]));
    $("#weapon-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.weaponKey)}</td>
          <td>${number(row.sortOrder)}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${assetPreview(row.imagePath, row.name)}</td>
          <td>
            <button onclick='editWeapon(${JSON.stringify(row)})'>편집</button>
            <button onclick='deleteWeapon(${JSON.stringify(row)})'>삭제</button>
          </td>
        </tr>`).join("");
}
function renderEnhancements() {
    const keyword = searchText("enhancement-search");
    const rows = (adminState.enhancementRules || []).filter(row => {
        if (!keyword) return true;
        return [`+${row.currentLevel}`, row.cost, row.successRate, row.keepRate, row.destroyRate]
            .some(value => String(value).toLowerCase().includes(keyword));
    });
    $("#enhancement-body").innerHTML = rows.map(row => `
        <tr>
          <td>+${row.currentLevel} -> +${row.currentLevel + 1}</td>
          <td>${number(row.cost)}</td>
          <td>${percent(row.successRate)}</td>
          <td>${percent(row.keepRate)}</td>
          <td>${percent(row.destroyRate)}</td>
          <td><button onclick='editEnhancement(${JSON.stringify(row)})'>편집</button></td>
        </tr>`).join("");
}

function renderEnhancementProof() {
    const proof = adminState.enhancementProof || { rows: [] };
    $("#enhancement-proof-summary").innerHTML = [
        ["전체 시도", number(proof.totalAttempts)],
        ["성공 비율", percent(proof.totalSuccessRate)],
        ["유지 비율", percent(proof.totalKeepRate)],
        ["파괴 비율", percent(proof.totalDestroyRate)]
    ].map(item => `<article class="admin-card"><span>${item[0]}</span><strong>${item[1]}</strong></article>`).join("");

    const rows = proof.rows || [];
    $("#enhancement-proof-body").innerHTML = rows.length
        ? rows.map(row => `
          <tr>
            <td>+${row.beforeLevel} -> +${row.beforeLevel + 1}</td>
            <td>${number(row.attempts)}</td>
            <td>${number(row.successCount)}회<br><small>${percent(row.actualSuccessRate)} / ${percent(row.expectedSuccessRate)}</small></td>
            <td>${number(row.keepCount)}회<br><small>${percent(row.actualKeepRate)} / ${percent(row.expectedKeepRate)}</small></td>
            <td>${number(row.destroyCount)}회<br><small>${percent(row.actualDestroyRate)} / ${percent(row.expectedDestroyRate)}</small></td>
            <td>${escapeHtml(row.lastAttemptedAt)}</td>
          </tr>`).join("")
        : `<tr><td colspan="6">아직 누적된 강화 시도 기록이 없습니다.</td></tr>`;
}

function renderAdminLogs(result) {
    const payload = result || { rows: [], totalRows: 0, page: 1, pageSize: logPageSize };
    const rows = payload.rows || [];
    adminLogPage = Number(payload.page || 1);
    $("#admin-log-body").innerHTML = rows.length
        ? rows.map(row => `
        <tr>
          <td>${escapeHtml(row.operatorPlayerKey)}</td>
          <td>${escapeHtml(row.actionType)}</td>
          <td>${escapeHtml(row.targetPlayerKey)}</td>
          <td>${escapeHtml(row.createdAt)}</td>
        </tr>`).join("")
        : `<tr><td colspan="4">조건에 맞는 관리자 로그가 없습니다.</td></tr>`;
    renderPagination("admin-log-pagination", payload.page, payload.pageSize, payload.totalRows, "setAdminLogPage");
}

function renderActionLogs(result) {
    const payload = result || { rows: [], totalRows: 0, page: 1, pageSize: logPageSize };
    const rows = payload.rows || [];
    actionLogPage = Number(payload.page || 1);
    $("#action-log-body").innerHTML = rows.length
        ? rows.map(row => `
          <tr>
            <td>${escapeHtml(row.nickname || row.playerKey)}<br><small>${escapeHtml(row.playerKey)}</small></td>
            <td>${escapeHtml(row.actionType)}</td>
            <td>${row.succeeded ? '<span class="admin-badge">성공</span>' : '<span class="admin-badge danger">실패</span>'}</td>
            <td>${escapeHtml(row.message)}</td>
            <td>
              <details>
                <summary>${escapeHtml(shortText(row.detailsJson || row.afterStateJson, 60))}</summary>
                <pre>${escapeHtml(`before: ${row.beforeStateJson}\nafter: ${row.afterStateJson}\ndetails: ${row.detailsJson || ""}`)}</pre>
              </details>
            </td>
            <td>${escapeHtml(row.createdAt)}</td>
          </tr>`).join("")
        : `<tr><td colspan="6">조건에 맞는 유저 행동 로그가 없습니다.</td></tr>`;
    renderPagination("action-log-pagination", payload.page, payload.pageSize, payload.totalRows, "setActionLogPage");
}

async function searchPlayers() {
    const rows = await adminApi(`search-players&q=${encodeURIComponent($("#user-search").value)}`);
    renderUsers(rows);
}

async function searchActionLogs() {
    await loadActionLogs(1);
}

async function loadActionLogs(page = actionLogPage) {
    const result = await adminApi(`search-action-logs&q=${encodeURIComponent($("#action-log-search").value)}&page=${page}&pageSize=${logPageSize}`);
    renderActionLogs(result);
}

async function loadAdminLogs(page = adminLogPage) {
    const result = await adminApi(`search-admin-logs&q=${encodeURIComponent($("#admin-log-search").value)}&page=${page}&pageSize=${logPageSize}`);
    renderAdminLogs(result);
}

async function setOperator(playerKey, isOperator) {
    await adminApi("set-operator", { targetPlayerKey: playerKey, isOperator });
    toast("운영자 권한을 변경했습니다.");
    await refreshDashboard();
    await loadAdminTab("users", true);
}

async function banUser(playerKey, isBanned) {
    const reason = isBanned ? prompt("차단 사유를 입력하세요.", "부정행위 확인") : "";
    if (isBanned && reason === null) return;
    await adminApi("set-ban", { targetPlayerKey: playerKey, isBanned, reason });
    toast("유저 접속 제한 상태를 변경했습니다.");
    await refreshDashboard();
    await loadAdminTab("users", true);
    if (loadedAdminTabs.has("abuse")) await loadAdminTab("abuse", true);
}

async function deleteMonster(row) {
    if (!confirm(`도감 데이터 '${row.name || row.monsterKey}' 항목을 삭제할까요?`)) return;
    await adminApi("delete-monster", { id: row.id, monsterKey: row.monsterKey });
    resetMonsterForm();
    toast("도감 데이터를 삭제했습니다.");
    await loadAdminTab("monsters", true);
}

async function deleteWeapon(row) {
    if (!confirm(`무기 데이터 '${row.name || row.weaponKey}' 항목을 삭제할까요?`)) return;
    await adminApi("delete-weapon", { id: row.id, weaponKey: row.weaponKey });
    resetWeaponForm();
    toast("무기 데이터를 삭제했습니다.");
    await loadAdminTab("weapons", true);
}
function editMonster(row) {
    $("#monster-id").value = row.id;
    $("#monster-key").value = row.monsterKey;
    $("#monster-area").value = row.areaId;
    $("#monster-grade").value = row.grade;
    $("#monster-slot").value = row.slotNumber;
    $("#monster-name").value = row.name;
    $("#monster-image").value = row.imagePath;
    $("#monster-sort").value = row.sortOrder;
    $("#monster-visible").value = String(row.isVisible);
    $("#monster-description").value = row.description;
}

function editWeapon(row) {
    $("#weapon-id").value = row.id;
    $("#weapon-key").value = row.weaponKey;
    $("#weapon-name").value = row.name;
    $("#weapon-image").value = row.imagePath;
    $("#weapon-sort").value = row.sortOrder;
    $("#weapon-visible").value = String(row.isVisible);
    $("#weapon-description").value = row.description;
}

function editEnhancement(row) {
    $("#enhancement-level").value = row.currentLevel;
    $("#enhancement-cost").value = row.cost;
    $("#enhancement-success").value = row.successRate;
    $("#enhancement-keep").value = row.keepRate;
    $("#enhancement-destroy").value = row.destroyRate;
    $("#enhancement-enabled").value = String(row.isEnabled);
}

function resetMonsterForm() {
    $("#monster-form").reset();
    $("#monster-id").value = "";
    $("#monster-grade").value = "normal";
    $("#monster-visible").value = "true";
}

function resetWeaponForm() {
    $("#weapon-form").reset();
    $("#weapon-id").value = "";
    $("#weapon-visible").value = "true";
}

function resetEnhancementForm() {
    $("#enhancement-form").reset();
    $("#enhancement-enabled").value = "true";
}

function setHotMultiplier(value) {
    $("#hot-gold").value = value;
    $("#hot-exp").value = value;
    updateHotTimeSummary();
}

function setHotDuration(hours) {
    const start = roundedKoreaNow();
    const end = new Date(start.getTime() + hours * 60 * 60 * 1000);
    $("#hot-enabled").value = "true";
    $("#hot-start").value = toLocalInputValue(start);
    $("#hot-end").value = toLocalInputValue(end);
    updateHotTimeSummary();
}

function setHotTonight() {
    const now = koreaNow();
    const start = new Date(now);
    start.setHours(20, 0, 0, 0);
    const end = new Date(now);
    end.setHours(22, 0, 0, 0);
    if (end <= now) {
        start.setDate(start.getDate() + 1);
        end.setDate(end.getDate() + 1);
    }
    $("#hot-enabled").value = "true";
    $("#hot-start").value = toLocalInputValue(start);
    $("#hot-end").value = toLocalInputValue(end);
    updateHotTimeSummary();
}

function updateHotTimeSummary() {
    const enabled = $("#hot-enabled").value === "true";
    const start = $("#hot-start").value;
    const end = $("#hot-end").value;
    const gold = $("#hot-gold").value || "1";
    const exp = $("#hot-exp").value || "1";
    const baseGold = $("#base-gold").value || "1";
    const baseExp = $("#base-exp").value || "1";
    const hotSummary = enabled
        ? `핫타임 한국시간 ${start || "(시작 미정)"} ~ ${end || "(종료 미정)"} / 골드 ${gold}배, 경험치 ${exp}배`
        : "핫타임이 꺼져 있습니다.";
    $("#hot-time-summary").textContent = `기본 골드 ${baseGold}배 · 기본 경험치 ${baseExp}배 / ${hotSummary}`;
}

function setRiftTestDuration(minutes) {
    const start = roundedKoreaNow();
    const end = new Date(start.getTime() + minutes * 60 * 1000);
    const settleEnd = new Date(end.getTime() + 5 * 60 * 1000);
    $("#rift-mode").value = "manual";
    $("#rift-enabled").value = "true";
    $("#rift-start").value = toLocalInputValue(start);
    $("#rift-end").value = toLocalInputValue(end);
    $("#rift-settle-end").value = toLocalInputValue(settleEnd);
    updateRiftSummary();
}

function updateRiftSummary() {
    const enabled = $("#rift-enabled")?.value === "true";
    const shopEnabled = $("#rift-shop-enabled")?.value === "true";
    const mode = $("#rift-mode")?.value || "auto";
    const start = $("#rift-start")?.value || "";
    const end = $("#rift-end")?.value || "";
    const settleEnd = $("#rift-settle-end")?.value || "";
    const boss = $("#rift-boss-area")?.value || "0";
    const summary = $("#rift-summary");
    if (!summary) return;
    summary.textContent = mode === "auto"
        ? `자동 주간 시즌 · 콘텐츠 ${enabled ? "ON" : "OFF"} · 상점 ${shopEnabled ? "ON" : "OFF"}`
        : `수동 테스트 · 사냥터 ${boss} 보스 · ${start || "시작 미정"} ~ ${end || "종료 미정"} · 정산 종료 ${settleEnd || "미정"}`;
}

async function settleRift() {
    if (!confirm("현재 주간 균열 시즌을 강제 정산할까요? 이미 정산된 시즌은 다시 정산할 수 없습니다.")) return;
    await adminApi("settle-rift", {});
    toast("현재 주간 균열 시즌을 정산했습니다.");
    await loadAdminTab("rift", true);
}

async function resetRift() {
    if (!confirm("현재 시즌의 유저 균열 데이터(피해량, 타격권, 주간 직접사냥)를 초기화할까요?")) return;
    await adminApi("reset-rift", {});
    toast("현재 주간 균열 유저 데이터를 초기화했습니다.");
    await loadAdminTab("rift", true);
}

async function clearRiftRewards() {
    if (!confirm("모든 유저의 주간 균열 랭킹 표식/테두리와 랭킹 보상 칭호를 제거할까요? 상점에서 구매한 칭호와 닉네임 색상은 유지됩니다.")) return;
    await adminApi("clear-rift-rewards", {});
    toast("주간 균열 랭킹 보상 효과를 제거했습니다.");
    await loadAdminTab("rift", true);
}

function koreaNow() {
    const formatter = new Intl.DateTimeFormat("sv-SE", {
        timeZone: "Asia/Seoul",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false
    });
    return new Date(formatter.format(new Date()).replace(" ", "T"));
}

function roundedKoreaNow() {
    const now = koreaNow();
    now.setSeconds(0, 0);
    return now;
}

function toLocalInputValue(date) {
    const pad = value => String(value).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toast(message) {
    $("#toast").textContent = message;
    $("#toast").classList.add("show");
    setTimeout(() => $("#toast").classList.remove("show"), 2600);
}

document.querySelectorAll("[data-admin-tab]").forEach(button => {
    button.addEventListener("click", async () => {
        document.querySelectorAll("[data-admin-tab], .admin-panel").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
        $(`#${button.dataset.adminTab}-panel`).classList.add("active");
        await loadAdminTab(button.dataset.adminTab).catch(error => toast(error.message));
    });
});

$("#hot-time-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-hottime", {
        enabled: $("#hot-enabled").value === "true",
        goldMultiplier: Number($("#hot-gold").value),
        experienceMultiplier: Number($("#hot-exp").value),
        baseGoldMultiplier: Number($("#base-gold").value),
        baseExperienceMultiplier: Number($("#base-exp").value),
        startsAtKst: $("#hot-start").value,
        endsAtKst: $("#hot-end").value
    });
    toast("핫타임 배율을 저장했습니다.");
    await refreshDashboard();
    await loadAdminTab("event", true);
});

$("#rift-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-rift", {
        enabled: $("#rift-enabled").value === "true",
        shopEnabled: $("#rift-shop-enabled").value === "true",
        mode: $("#rift-mode").value,
        manualSeasonName: $("#rift-season-name").value,
        manualStartsAtKst: $("#rift-start").value,
        manualEndsAtKst: $("#rift-end").value,
        manualSettlementEndsAtKst: $("#rift-settle-end").value,
        manualBossAreaId: Number($("#rift-boss-area").value || 0)
    });
    toast("주간 균열 설정을 저장했습니다.");
    await loadAdminTab("rift", true);
});

["hot-enabled", "hot-gold", "hot-exp", "base-gold", "base-exp", "hot-start", "hot-end"].forEach(id => {
    const element = $("#" + id);
    element.addEventListener("input", updateHotTimeSummary);
    element.addEventListener("change", updateHotTimeSummary);
});

["rift-enabled", "rift-shop-enabled", "rift-mode", "rift-season-name", "rift-start", "rift-end", "rift-settle-end", "rift-boss-area"].forEach(id => {
    const element = $("#" + id);
    if (!element) return;
    element.addEventListener("input", updateRiftSummary);
    element.addEventListener("change", updateRiftSummary);
});

[
    ["abuse-search", renderSuspicious],
    ["monster-search", renderMonsters],
    ["weapon-search", renderWeapons],
    ["enhancement-search", renderEnhancements],
    ["action-log-search", () => {}],
    ["admin-log-search", () => loadAdminLogs(1)]
].forEach(([id, render]) => {
    const element = $("#" + id);
    if (element) element.addEventListener("input", render);
});

$("#user-search").addEventListener("keydown", event => {
    if (event.key === "Enter") searchPlayers();
});

$("#action-log-search").addEventListener("keydown", event => {
    if (event.key === "Enter") searchActionLogs();
});

$("#monster-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-monster", {
        id: Number($("#monster-id").value || 0),
        monsterKey: $("#monster-key").value,
        areaId: Number($("#monster-area").value),
        grade: $("#monster-grade").value,
        slotNumber: Number($("#monster-slot").value),
        name: $("#monster-name").value,
        imagePath: $("#monster-image").value,
        sortOrder: Number($("#monster-sort").value || 0),
        isVisible: $("#monster-visible").value === "true",
        description: $("#monster-description").value
    });
    toast("도감 데이터를 저장했습니다.");
    resetMonsterForm();
    await loadAdminTab("monsters", true);
});

$("#enhancement-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-enhancement", {
        currentLevel: Number($("#enhancement-level").value),
        cost: Number($("#enhancement-cost").value),
        successRate: Number($("#enhancement-success").value),
        keepRate: Number($("#enhancement-keep").value),
        destroyRate: Number($("#enhancement-destroy").value),
        isEnabled: $("#enhancement-enabled").value === "true"
    });
    toast("강화 확률을 저장했습니다.");
    resetEnhancementForm();
    await loadAdminTab("enhancements", true);
});

$("#weapon-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-weapon", {
        id: Number($("#weapon-id").value || 0),
        weaponKey: $("#weapon-key").value,
        name: $("#weapon-name").value,
        imagePath: $("#weapon-image").value,
        sortOrder: Number($("#weapon-sort").value || 0),
        isVisible: $("#weapon-visible").value === "true",
        description: $("#weapon-description").value
    });
    toast("무기 데이터를 저장했습니다.");
    resetWeaponForm();
    await loadAdminTab("weapons", true);
});

loadAdmin().catch(error => toast(error.message));
