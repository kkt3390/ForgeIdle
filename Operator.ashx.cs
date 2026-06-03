using System;
using System.Web;
using System.Web.SessionState;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms
{
    public sealed class OperatorHandler : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        // 일부 호스팅에서 새 .aspx 파일 반영이 늦거나 차단되어 .ashx로 운영자 화면을 제공합니다.
        public void ProcessRequest(HttpContext context)
        {
            try
            {
                new AdminRepository().RequireOperator(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Write(exception.Message);
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Write(Html);
        }

        private const string Html = @"<!doctype html>
<html lang=""ko"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>강화중독 운영자</title>
  <link rel=""stylesheet"" href=""Content/site.css"" />
  <style>
    .admin-grid { display:grid; grid-template-columns:repeat(4,1fr); gap:10px; }
    .admin-card { border:1px solid #ffffff12; border-radius:14px; padding:14px; background:#11131a; }
    .admin-card strong { display:block; color:#ffd27c; font-size:22px; }
    .admin-layout { display:grid; grid-template-columns:220px 1fr; gap:14px; margin-top:16px; }
    .admin-menu { display:grid; gap:8px; align-content:start; }
    .admin-menu button { text-align:left; }
    .admin-menu button.active { color:#25190a; background:#f0b85c; }
    .admin-panel { display:none; }
    .admin-panel.active { display:block; }
    .admin-form { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:10px; }
    .admin-form label { display:grid; gap:5px; color:#c4b8aa; font-size:13px; }
    .admin-form input, .admin-form select, .admin-form textarea { border:1px solid #ffffff24; border-radius:8px; padding:8px 9px; color:#f5e6d0; background:#0e1016; }
    .admin-form textarea { min-height:70px; resize:vertical; }
    .admin-form .full { grid-column:1 / -1; }
    .admin-actions { display:flex; flex-wrap:wrap; gap:8px; margin:10px 0; }
    .admin-table-wrap { overflow-x:auto; margin-top:10px; }
    .admin-badge { display:inline-block; border-radius:999px; padding:3px 7px; background:#ffffff12; font-size:11px; }
    .admin-badge.warn { color:#25190a; background:#f0b85c; }
    .admin-badge.danger { color:#fff; background:#9f3636; }
    @media (max-width:720px) { .admin-grid { grid-template-columns:repeat(2,1fr); } .admin-layout { grid-template-columns:1fr; } .admin-form { grid-template-columns:1fr; } }
  </style>
</head>
<body>
  <main class=""shell"">
    <header class=""topbar""><div><p class=""eyebrow"">OPERATOR CENTER</p><h1>운영자 관리</h1></div><a class=""logout"" href=""Default.aspx"">게임으로</a></header>
    <section class=""admin-grid"" id=""dashboard""></section>
    <section class=""admin-layout"">
      <nav class=""admin-menu"">
        <button class=""tab active"" data-admin-tab=""abuse"">이상행동 감지</button>
        <button class=""tab"" data-admin-tab=""users"">유저/권한/밴</button>
        <button class=""tab"" data-admin-tab=""event"">핫타임 배율</button>
        <button class=""tab"" data-admin-tab=""monsters"">도감 관리</button>
        <button class=""tab"" data-admin-tab=""weapons"">무기 관리</button>
        <button class=""tab"" data-admin-tab=""logs"">관리자 로그</button>
      </nav>
      <div>
        <section class=""panel admin-panel active"" id=""abuse-panel""><div class=""section-title""><h2>실시간 이상행동 후보</h2><span>최근 7일 로그 기준</span></div><p class=""collection-description"">비정상 재화, 경험치, 강화도, 보호권 값을 자동 후보로 올립니다. 최종 판단 후 유저 차단 버튼으로 조치하세요.</p><div class=""admin-table-wrap""><table><thead><tr><th>유저</th><th>행동</th><th>사유</th><th>시간</th><th>조치</th></tr></thead><tbody id=""abuse-body""></tbody></table></div></section>
        <section class=""panel admin-panel"" id=""users-panel""><div class=""section-title""><h2>유저 검색과 권한</h2><span>운영자 지정 / 접속 차단</span></div><div class=""admin-actions""><input id=""user-search"" placeholder=""닉네임 또는 PlayerKey"" /><button onclick=""searchPlayers()"">검색</button></div><div class=""admin-table-wrap""><table><thead><tr><th>유저</th><th>상태</th><th>레벨</th><th>골드</th><th>강화</th><th>조치</th></tr></thead><tbody id=""users-body""></tbody></table></div></section>
        <section class=""panel admin-panel"" id=""event-panel""><div class=""section-title""><h2>핫타임 배율</h2><span>서버 보상 계산에 즉시 반영</span></div><form class=""admin-form"" id=""hot-time-form""><label>사용 여부<select id=""hot-enabled""><option value=""true"">켜기</option><option value=""false"">끄기</option></select></label><label>골드 배율<input id=""hot-gold"" type=""number"" step=""0.1"" min=""0.1"" max=""20"" /></label><label>경험치 배율<input id=""hot-exp"" type=""number"" step=""0.1"" min=""0.1"" max=""20"" /></label><label>시작 UTC<input id=""hot-start"" placeholder=""2026-06-03T12:00:00Z"" /></label><label>종료 UTC<input id=""hot-end"" placeholder=""2026-06-03T14:00:00Z"" /></label><button class=""primary full"" type=""submit"">핫타임 저장</button></form></section>
        <section class=""panel admin-panel"" id=""monsters-panel""><div class=""section-title""><h2>도감 데이터</h2><span>이미지/이름/설명/순번 관리</span></div><form class=""admin-form"" id=""monster-form""><input type=""hidden"" id=""monster-id"" /><label>몬스터 키<input id=""monster-key"" placeholder=""area-00-normal-01"" /></label><label>사냥터 ID<input id=""monster-area"" type=""number"" min=""0"" max=""11"" /></label><label>등급<select id=""monster-grade""><option value=""normal"">일반</option><option value=""elite"">정예</option><option value=""golden"">황금</option></select></label><label>순번<input id=""monster-slot"" type=""number"" min=""1"" /></label><label>이름<input id=""monster-name"" /></label><label>이미지 경로<input id=""monster-image"" placeholder=""Content/monsters/..."" /></label><label>정렬 순서<input id=""monster-sort"" type=""number"" /></label><label>표시 여부<select id=""monster-visible""><option value=""true"">표시</option><option value=""false"">숨김</option></select></label><label class=""full"">설명<textarea id=""monster-description""></textarea></label><button class=""primary full"" type=""submit"">도감 저장</button></form><div class=""admin-table-wrap""><table><thead><tr><th>키</th><th>구성</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id=""monster-body""></tbody></table></div></section>
        <section class=""panel admin-panel"" id=""weapons-panel""><div class=""section-title""><h2>무기 데이터</h2><span>무기 종류 확장용</span></div><form class=""admin-form"" id=""weapon-form""><input type=""hidden"" id=""weapon-id"" /><label>무기 키<input id=""weapon-key"" placeholder=""basic-sword"" /></label><label>이름<input id=""weapon-name"" /></label><label>이미지 경로<input id=""weapon-image"" /></label><label>정렬 순서<input id=""weapon-sort"" type=""number"" /></label><label>표시 여부<select id=""weapon-visible""><option value=""true"">표시</option><option value=""false"">숨김</option></select></label><label class=""full"">설명<textarea id=""weapon-description""></textarea></label><button class=""primary full"" type=""submit"">무기 저장</button></form><div class=""admin-table-wrap""><table><thead><tr><th>키</th><th>이름</th><th>이미지</th><th>편집</th></tr></thead><tbody id=""weapon-body""></tbody></table></div></section>
        <section class=""panel admin-panel"" id=""logs-panel""><div class=""section-title""><h2>관리자 행동 로그</h2><span>권한/배율/콘텐츠 변경 기록</span></div><div class=""admin-table-wrap""><table><thead><tr><th>운영자</th><th>행동</th><th>대상</th><th>시간</th></tr></thead><tbody id=""admin-log-body""></tbody></table></div></section>
      </div>
    </section>
  </main>
  <div class=""toast"" id=""toast""></div>
  <script src=""Scripts/operator.js?v=20260603-1""></script>
</body>
</html>";
    }
}
