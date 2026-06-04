let adminState = null;

const $ = selector => document.querySelector(selector);
const number = value => Number(value || 0).toLocaleString("ko-KR");
const percent = value => `${(Number(value || 0) * 100).toLocaleString("ko-KR", { maximumFractionDigits: 4 })}%`;
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
    renderHotTime();
    renderSuspicious();
    renderUsers(adminState.operators);
    renderEnhancements();
    renderMonsters();
    renderWeapons();
    renderAdminLogs();
}

function renderDashboard() {
    const dashboard = adminState.dashboard;
    const hot = adminState.hotTime;
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
    $("#hot-start").value = hot.startsAtKst || "";
    $("#hot-end").value = hot.endsAtKst || "";
    updateHotTimeSummary();
}

function renderSuspicious() {
    const keyword = searchText("abuse-search");
    const rows = adminState.suspiciousUsers.filter(row =>
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
    const rows = adminState.monsterCatalog.filter(row =>
        matchesKeyword(row, keyword, ["monsterKey", "grade", "name", "imagePath", "description", "sortOrder"]));
    $("#monster-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.monsterKey)}</td>
          <td>${row.areaId} / ${escapeHtml(row.grade)} / ${row.slotNumber}</td>
          <td>${number(row.sortOrder)}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${assetPreview(row.imagePath, row.name)}</td>
          <td><button onclick='editMonster(${JSON.stringify(row)})'>편집</button></td>
        </tr>`).join("");
}
function renderWeapons() {
    const keyword = searchText("weapon-search");
    const rows = adminState.weaponCatalog.filter(row =>
        matchesKeyword(row, keyword, ["weaponKey", "name", "imagePath", "description", "sortOrder"]));
    $("#weapon-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.weaponKey)}</td>
          <td>${number(row.sortOrder)}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${assetPreview(row.imagePath, row.name)}</td>
          <td><button onclick='editWeapon(${JSON.stringify(row)})'>편집</button></td>
        </tr>`).join("");
}
function renderEnhancements() {
    const keyword = searchText("enhancement-search");
    const rows = adminState.enhancementRules.filter(row => {
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

function renderAdminLogs() {
    const keyword = searchText("admin-log-search");
    const rows = adminState.recentAdminLogs.filter(row =>
        matchesKeyword(row, keyword, ["operatorPlayerKey", "actionType", "targetPlayerKey", "createdAt"]));
    $("#admin-log-body").innerHTML = rows.map(row => `
        <tr>
          <td>${escapeHtml(row.operatorPlayerKey)}</td>
          <td>${escapeHtml(row.actionType)}</td>
          <td>${escapeHtml(row.targetPlayerKey)}</td>
          <td>${escapeHtml(row.createdAt)}</td>
        </tr>`).join("");
}

async function searchPlayers() {
    const rows = await adminApi(`search-players&q=${encodeURIComponent($("#user-search").value)}`);
    renderUsers(rows);
}

async function setOperator(playerKey, isOperator) {
    await adminApi("set-operator", { targetPlayerKey: playerKey, isOperator });
    toast("운영자 권한을 변경했습니다.");
    await loadAdmin();
}

async function banUser(playerKey, isBanned) {
    const reason = isBanned ? prompt("차단 사유를 입력하세요.", "부정행위 확인") : "";
    if (isBanned && reason === null) return;
    await adminApi("set-ban", { targetPlayerKey: playerKey, isBanned, reason });
    toast("유저 접속 제한 상태를 변경했습니다.");
    await loadAdmin();
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
    $("#hot-time-summary").textContent = enabled
        ? `한국시간 ${start || "(시작 미지정)"} ~ ${end || "(종료 미지정)"} / 골드 ${gold}배, 경험치 ${exp}배`
        : "핫타임이 꺼져 있습니다.";
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
    button.addEventListener("click", () => {
        document.querySelectorAll("[data-admin-tab], .admin-panel").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
        $(`#${button.dataset.adminTab}-panel`).classList.add("active");
    });
});

$("#hot-time-form").addEventListener("submit", async event => {
    event.preventDefault();
    await adminApi("save-hottime", {
        enabled: $("#hot-enabled").value === "true",
        goldMultiplier: Number($("#hot-gold").value),
        experienceMultiplier: Number($("#hot-exp").value),
        startsAtKst: $("#hot-start").value,
        endsAtKst: $("#hot-end").value
    });
    toast("핫타임 배율을 저장했습니다.");
    await loadAdmin();
});

["hot-enabled", "hot-gold", "hot-exp", "hot-start", "hot-end"].forEach(id => {
    const element = $("#" + id);
    element.addEventListener("input", updateHotTimeSummary);
    element.addEventListener("change", updateHotTimeSummary);
});

[
    ["abuse-search", renderSuspicious],
    ["monster-search", renderMonsters],
    ["weapon-search", renderWeapons],
    ["enhancement-search", renderEnhancements],
    ["admin-log-search", renderAdminLogs]
].forEach(([id, render]) => {
    const element = $("#" + id);
    if (element) element.addEventListener("input", render);
});

$("#user-search").addEventListener("keydown", event => {
    if (event.key === "Enter") searchPlayers();
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
    await loadAdmin();
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
    await loadAdmin();
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
    await loadAdmin();
});

loadAdmin().catch(error => toast(error.message));
