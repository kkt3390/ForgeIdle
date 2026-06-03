<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Admin.aspx.cs" Inherits="EnhanceAddiction.WebForms.Admin" %>
<!doctype html>
<html lang="ko">
<head runat="server">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>강화중독 운영자</title>
  <link rel="stylesheet" href="Content/site.css" />
  <style>
    .admin-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; }
    .admin-card { border: 1px solid #ffffff12; border-radius: 14px; padding: 14px; background: #11131a; }
    .admin-card strong { display: block; color: #ffd27c; font-size: 22px; }
    .admin-layout { display: grid; grid-template-columns: 220px 1fr; gap: 14px; margin-top: 16px; }
    .admin-menu { display: grid; gap: 8px; align-content: start; }
    .admin-menu button { text-align: left; }
    .admin-menu button.active { color: #25190a; background: #f0b85c; }
    .admin-panel { display: none; }
    .admin-panel.active { display: block; }
    .admin-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .admin-form label { display: grid; gap: 5px; color: #c4b8aa; font-size: 13px; }
    .admin-form input, .admin-form select, .admin-form textarea {
      border: 1px solid #ffffff24; border-radius: 8px; padding: 8px 9px;
      color: #f5e6d0; background: #0e1016;
    }
    .admin-form textarea { min-height: 70px; resize: vertical; }
    .admin-form .full { grid-column: 1 / -1; }
    .admin-actions { display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0; }
    .admin-table-wrap { overflow-x: auto; margin-top: 10px; }
    .admin-badge { display: inline-block; border-radius: 999px; padding: 3px 7px; background: #ffffff12; font-size: 11px; }
    .admin-badge.warn { color: #25190a; background: #f0b85c; }
    .admin-badge.danger { color: #fff; background: #9f3636; }
    @media (max-width: 720px) {
      .admin-grid { grid-template-columns: repeat(2, 1fr); }
      .admin-layout { grid-template-columns: 1fr; }
      .admin-form { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <main class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">OPERATOR CENTER</p>
        <h1>운영자 관리</h1>
      </div>
      <a class="logout" href="Default.aspx">게임으로</a>
    </header>

    <section class="admin-grid" id="dashboard"></section>

    <section class="admin-layout">
      <nav class="admin-menu">
        <button class="tab active" data-admin-tab="abuse">이상행동 감지</button>
        <button class="tab" data-admin-tab="users">유저/권한/밴</button>
        <button class="tab" data-admin-tab="event">핫타임 배율</button>
        <button class="tab" data-admin-tab="monsters">도감 관리</button>
        <button class="tab" data-admin-tab="weapons">무기 관리</button>
        <button class="tab" data-admin-tab="logs">관리자 로그</button>
      </nav>

      <div>
        <section class="panel admin-panel active" id="abuse-panel">
          <div class="section-title"><h2>실시간 이상행동 후보</h2><span>최근 7일 로그 기준</span></div>
          <p class="collection-description">비정상 재화, 경험치, 강화도, 보호권 값을 자동 후보로 올립니다. 최종 판단 후 유저 차단 버튼으로 조치하세요.</p>
          <div class="admin-table-wrap"><table><thead><tr><th>유저</th><th>행동</th><th>사유</th><th>시간</th><th>조치</th></tr></thead><tbody id="abuse-body"></tbody></table></div>
        </section>

        <section class="panel admin-panel" id="users-panel">
          <div class="section-title"><h2>유저 검색과 권한</h2><span>운영자 지정 / 접속 차단</span></div>
          <div class="admin-actions">
            <input id="user-search" placeholder="닉네임 또는 PlayerKey" />
            <button onclick="searchPlayers()">검색</button>
          </div>
          <div class="admin-table-wrap"><table><thead><tr><th>유저</th><th>상태</th><th>레벨</th><th>골드</th><th>강화</th><th>조치</th></tr></thead><tbody id="users-body"></tbody></table></div>
        </section>

        <section class="panel admin-panel" id="event-panel">
          <div class="section-title"><h2>핫타임 배율</h2><span>서버 보상 계산에 즉시 반영</span></div>
          <form class="admin-form" id="hot-time-form">
            <label>사용 여부<select id="hot-enabled"><option value="true">켜기</option><option value="false">끄기</option></select></label>
            <label>골드 배율<input id="hot-gold" type="number" step="0.1" min="0.1" max="20" /></label>
            <label>경험치 배율<input id="hot-exp" type="number" step="0.1" min="0.1" max="20" /></label>
            <label>시작 UTC<input id="hot-start" placeholder="2026-06-03T12:00:00Z" /></label>
            <label>종료 UTC<input id="hot-end" placeholder="2026-06-03T14:00:00Z" /></label>
            <button class="primary full" type="submit">핫타임 저장</button>
          </form>
        </section>

        <section class="panel admin-panel" id="monsters-panel">
          <div class="section-title"><h2>도감 데이터</h2><span>이미지/이름/설명/순번 관리</span></div>
          <form class="admin-form" id="monster-form">
            <input type="hidden" id="monster-id" />
            <label>몬스터 키<input id="monster-key" placeholder="area-00-normal-01" /></label>
            <label>사냥터 ID<input id="monster-area" type="number" min="0" max="11" /></label>
            <label>등급<select id="monster-grade"><option value="normal">일반</option><option value="elite">정예</option><option value="golden">황금</option></select></label>
            <label>순번<input id="monster-slot" type="number" min="1" /></label>
            <label>이름<input id="monster-name" /></label>
            <label>이미지 경로<input id="monster-image" placeholder="Content/monsters/..." /></label>
            <label>정렬 순서<input id="monster-sort" type="number" /></label>
            <label>표시 여부<select id="monster-visible"><option value="true">표시</option><option value="false">숨김</option></select></label>
            <label class="full">설명<textarea id="monster-description"></textarea></label>
            <button class="primary full" type="submit">도감 저장</button>
          </form>
          <div class="admin-table-wrap"><table><thead><tr><th>키</th><th>구성</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id="monster-body"></tbody></table></div>
        </section>

        <section class="panel admin-panel" id="weapons-panel">
          <div class="section-title"><h2>무기 데이터</h2><span>무기 종류 확장용</span></div>
          <form class="admin-form" id="weapon-form">
            <input type="hidden" id="weapon-id" />
            <label>무기 키<input id="weapon-key" placeholder="basic-sword" /></label>
            <label>이름<input id="weapon-name" /></label>
            <label>이미지 경로<input id="weapon-image" /></label>
            <label>정렬 순서<input id="weapon-sort" type="number" /></label>
            <label>표시 여부<select id="weapon-visible"><option value="true">표시</option><option value="false">숨김</option></select></label>
            <label class="full">설명<textarea id="weapon-description"></textarea></label>
            <button class="primary full" type="submit">무기 저장</button>
          </form>
          <div class="admin-table-wrap"><table><thead><tr><th>키</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id="weapon-body"></tbody></table></div>
        </section>

        <section class="panel admin-panel" id="logs-panel">
          <div class="section-title"><h2>관리자 행동 로그</h2><span>권한/배율/콘텐츠 변경 기록</span></div>
          <div class="admin-table-wrap"><table><thead><tr><th>운영자</th><th>행동</th><th>대상</th><th>시간</th></tr></thead><tbody id="admin-log-body"></tbody></table></div>
        </section>
      </div>
    </section>
  </main>

  <div class="toast" id="toast"></div>
  <script>
    let adminState = null;
    const $ = selector => document.querySelector(selector);
    const number = value => Number(value || 0).toLocaleString("ko-KR");
    const escapeHtml = value => {
      const div = document.createElement("div");
      div.textContent = value ?? "";
      return div.innerHTML;
    };
    async function adminApi(action, body) {
      const response = await fetch(`Api/AdminApi.ashx?action=${action}`, {
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
      $("#hot-start").value = hot.startsAtUtc || "";
      $("#hot-end").value = hot.endsAtUtc || "";
    }
    function renderSuspicious() {
      $("#abuse-body").innerHTML = adminState.suspiciousUsers.length
        ? adminState.suspiciousUsers.map(row => `
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
            ${row.isBanned ? '<span class="admin-badge danger">차단</span>' : ''}
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
      $("#monster-body").innerHTML = adminState.monsterCatalog.map(row => `
        <tr>
          <td>${escapeHtml(row.monsterKey)}</td>
          <td>${row.areaId} / ${escapeHtml(row.grade)} / ${row.slotNumber}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${escapeHtml(row.imagePath)}</td>
          <td><button onclick='editMonster(${JSON.stringify(row)})'>편집</button></td>
        </tr>`).join("");
    }
    function renderWeapons() {
      $("#weapon-body").innerHTML = adminState.weaponCatalog.map(row => `
        <tr>
          <td>${escapeHtml(row.weaponKey)}</td>
          <td>${escapeHtml(row.name)}</td>
          <td>${escapeHtml(row.imagePath)}</td>
          <td><button onclick='editWeapon(${JSON.stringify(row)})'>편집</button></td>
        </tr>`).join("");
    }
    function renderAdminLogs() {
      $("#admin-log-body").innerHTML = adminState.recentAdminLogs.map(row => `
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
        startsAtUtc: $("#hot-start").value,
        endsAtUtc: $("#hot-end").value
      });
      toast("핫타임 배율을 저장했습니다.");
      await loadAdmin();
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
  </script>
</body>
</html>
