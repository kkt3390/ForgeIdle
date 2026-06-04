namespace EnhanceAddiction.WebForms.Api
{
    public static class OperatorPageTemplate
    {
        public static string PageHtml { get { return Html; } }

        private const string Html = @"<!doctype html>
<html lang=""ko"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>강화중독 운영자</title>
  <link rel=""stylesheet"" href=""/Content/site.css?v=20260604-1"" />
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
    .admin-form input, .admin-form select, .admin-form textarea, .admin-actions input {
      border: 1px solid #ffffff24;
      border-radius: 8px;
      padding: 8px 9px;
      color: #f5e6d0;
      background: #0e1016;
    }
    .admin-form textarea { min-height: 70px; resize: vertical; }
    .admin-form .full { grid-column: 1 / -1; }
    .admin-actions { display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0; }
    .admin-actions input { min-width: 260px; flex: 1; }
    .admin-table-wrap { overflow-x: auto; margin-top: 10px; }
        .admin-image-cell { display: flex; align-items: center; gap: 8px; min-width: 220px; }
        .admin-thumb { width: 42px; height: 42px; flex: 0 0 auto; object-fit: contain; border: 1px solid #ffffff24; border-radius: 8px; background: #11131a; }
        .admin-muted { color: #7f756d; font-size: 12px; }
    .admin-badge { display: inline-block; border-radius: 999px; padding: 3px 7px; background: #ffffff12; font-size: 11px; }
    .admin-badge.warn { color: #25190a; background: #f0b85c; }
    .admin-badge.danger { color: #fff; background: #9f3636; }
    .hot-preset-row { display: flex; flex-wrap: wrap; gap: 8px; }
    .hot-preset-row button { padding: 9px 11px; font-size: 12px; }
    @media (max-width: 720px) {
      .admin-grid { grid-template-columns: repeat(2, 1fr); }
      .admin-layout { grid-template-columns: 1fr; }
      .admin-form { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <main class=""shell"">
    <header class=""topbar"">
      <div>
        <p class=""eyebrow"">OPERATOR CENTER</p>
        <h1>운영자 관리</h1>
      </div>
      <a class=""logout"" href=""/Default.aspx"">게임으로</a>
    </header>

    <section class=""admin-grid"" id=""dashboard""></section>

    <section class=""admin-layout"">
      <nav class=""admin-menu"">
        <button class=""tab active"" data-admin-tab=""abuse"">이상행동 감지</button>
        <button class=""tab"" data-admin-tab=""users"">유저/권한/밴</button>
        <button class=""tab"" data-admin-tab=""event"">핫타임 배율</button>
        <button class=""tab"" data-admin-tab=""enhancements"">강화 확률</button>
        <button class=""tab"" data-admin-tab=""monsters"">도감 관리</button>
        <button class=""tab"" data-admin-tab=""weapons"">무기 관리</button>
        <button class=""tab"" data-admin-tab=""logs"">관리자 로그</button>
      </nav>

      <div>
        <section class=""panel admin-panel active"" id=""abuse-panel"">
          <div class=""section-title""><h2>실시간 이상행동 알림</h2><span>최근 7일 로그 기준</span></div>
          <p class=""collection-description"">비정상 골드, 경험치, 강화도, 보호권 값을 자동 알림으로 올립니다.</p>
          <div class=""admin-actions""><input id=""abuse-search"" placeholder=""닉네임, PlayerKey, 행동, 사유 검색"" /></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>유저</th><th>행동</th><th>사유</th><th>시간</th><th>조치</th></tr></thead><tbody id=""abuse-body""></tbody></table></div>
        </section>

        <section class=""panel admin-panel"" id=""users-panel"">
          <div class=""section-title""><h2>유저 검색과 권한</h2><span>운영자 지정 / 접속 차단</span></div>
          <div class=""admin-actions""><input id=""user-search"" placeholder=""닉네임 또는 PlayerKey"" /><button onclick=""searchPlayers()"">검색</button></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>유저</th><th>상태</th><th>레벨</th><th>골드</th><th>강화</th><th>조치</th></tr></thead><tbody id=""users-body""></tbody></table></div>
        </section>

        <section class=""panel admin-panel"" id=""event-panel"">
          <div class=""section-title""><h2>핫타임 배율</h2><span>한국시간 기준</span></div>
          <form class=""admin-form"" id=""hot-time-form"">
            <label>사용 여부<select id=""hot-enabled""><option value=""true"">켜기</option><option value=""false"">끄기</option></select></label>
            <label>골드 배율<input id=""hot-gold"" type=""number"" step=""0.1"" min=""0.1"" max=""20"" /></label>
            <label>경험치 배율<input id=""hot-exp"" type=""number"" step=""0.1"" min=""0.1"" max=""20"" /></label>
            <div class=""hot-preset-row full""><button type=""button"" onclick=""setHotMultiplier(1)"">1배</button><button type=""button"" onclick=""setHotMultiplier(2)"">2배</button><button type=""button"" onclick=""setHotMultiplier(3)"">3배</button><button type=""button"" onclick=""setHotMultiplier(5)"">5배</button></div>
            <label>시작 시간<input id=""hot-start"" type=""datetime-local"" /></label>
            <label>종료 시간<input id=""hot-end"" type=""datetime-local"" /></label>
            <div class=""hot-preset-row full""><button type=""button"" onclick=""setHotDuration(1)"">지금부터 1시간</button><button type=""button"" onclick=""setHotDuration(2)"">지금부터 2시간</button><button type=""button"" onclick=""setHotDuration(4)"">지금부터 4시간</button><button type=""button"" onclick=""setHotTonight()"">오늘 20~22시</button></div>
            <p class=""collection-description full"" id=""hot-time-summary""></p>
            <button class=""primary full"" type=""submit"">핫타임 저장</button>
          </form>
        </section>

        <section class=""panel admin-panel"" id=""enhancements-panel"">
          <div class=""section-title""><h2>강화 확률</h2><span>저장 즉시 반영</span></div>
          <form class=""admin-form"" id=""enhancement-form"">
            <label>현재 단계<input id=""enhancement-level"" type=""number"" min=""0"" max=""29"" /></label>
            <label>비용<input id=""enhancement-cost"" type=""number"" min=""0"" /></label>
            <label>성공 확률<input id=""enhancement-success"" type=""number"" step=""0.0001"" min=""0"" max=""1"" /></label>
            <label>유지 확률<input id=""enhancement-keep"" type=""number"" step=""0.0001"" min=""0"" max=""1"" /></label>
            <label>파괴 확률<input id=""enhancement-destroy"" type=""number"" step=""0.0001"" min=""0"" max=""1"" /></label>
            <label>사용 여부<select id=""enhancement-enabled""><option value=""true"">사용</option><option value=""false"">비활성</option></select></label>
            <button class=""primary full"" type=""submit"">강화 확률 저장</button>
          </form>
          <p class=""collection-description"">확률은 0.3 = 30% 형식으로 입력합니다. 세 확률의 합은 1이어야 합니다.</p>
          <div class=""admin-actions""><input id=""enhancement-search"" placeholder=""강화 단계, 비용, 확률 검색"" /></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>단계</th><th>비용</th><th>성공</th><th>유지</th><th>파괴</th><th>편집</th></tr></thead><tbody id=""enhancement-body""></tbody></table></div>
        </section>

        <section class=""panel admin-panel"" id=""monsters-panel"">
          <div class=""section-title""><h2>도감 데이터</h2><span>이미지/이름/설명/순번 관리</span></div>
          <form class=""admin-form"" id=""monster-form"">
            <input type=""hidden"" id=""monster-id"" />
            <label>몬스터 키<input id=""monster-key"" placeholder=""area-00-normal-01"" /></label>
            <label>사냥터 ID<input id=""monster-area"" type=""number"" min=""0"" max=""11"" /></label>
            <label>등급<select id=""monster-grade""><option value=""normal"">일반</option><option value=""elite"">정예</option><option value=""golden"">황금</option></select></label>
            <label>순번<input id=""monster-slot"" type=""number"" min=""1"" /></label>
            <label>이름<input id=""monster-name"" /></label>
            <label>이미지 경로<input id=""monster-image"" placeholder=""Content/monsters/..."" /></label>
            <label>정렬 순서<input id=""monster-sort"" type=""number"" /></label>
            <label>표시 여부<select id=""monster-visible""><option value=""true"">표시</option><option value=""false"">숨김</option></select></label>
            <label class=""full"">설명<textarea id=""monster-description""></textarea></label>
            <button class=""primary full"" type=""submit"">도감 저장</button>
          </form>
          <div class=""admin-actions""><input id=""monster-search"" placeholder=""몬스터 키, 이름, 등급, 이미지 검색"" /></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>키</th><th>구성</th><th>정렬</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id=""monster-body""></tbody></table></div>
        </section>

        <section class=""panel admin-panel"" id=""weapons-panel"">
          <div class=""section-title""><h2>무기 데이터</h2><span>무기 종류 확장용</span></div>
          <form class=""admin-form"" id=""weapon-form"">
            <input type=""hidden"" id=""weapon-id"" />
            <label>무기 키<input id=""weapon-key"" placeholder=""basic-sword"" /></label>
            <label>이름<input id=""weapon-name"" /></label>
            <label>이미지 경로<input id=""weapon-image"" /></label>
            <label>정렬 순서<input id=""weapon-sort"" type=""number"" /></label>
            <label>표시 여부<select id=""weapon-visible""><option value=""true"">표시</option><option value=""false"">숨김</option></select></label>
            <label class=""full"">설명<textarea id=""weapon-description""></textarea></label>
            <button class=""primary full"" type=""submit"">무기 저장</button>
          </form>
          <div class=""admin-actions""><input id=""weapon-search"" placeholder=""무기 키, 이름, 이미지 검색"" /></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>키</th><th>정렬</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id=""weapon-body""></tbody></table></div>
        </section>

        <section class=""panel admin-panel"" id=""logs-panel"">
          <div class=""section-title""><h2>관리자 행동 로그</h2><span>권한/배율/콘텐츠 변경 기록</span></div>
          <div class=""admin-actions""><input id=""admin-log-search"" placeholder=""운영자, 행동, 대상 검색"" /></div>
          <div class=""admin-table-wrap""><table><thead><tr><th>운영자</th><th>행동</th><th>대상</th><th>시간</th></tr></thead><tbody id=""admin-log-body""></tbody></table></div>
        </section>
      </div>
    </section>
  </main>

  <div class=""toast"" id=""toast""></div>
  <script src=""/Scripts/operator.js?v=20260604-3""></script>
</body>
</html>";
    }
}
