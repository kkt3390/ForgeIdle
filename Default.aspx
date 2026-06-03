<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="EnhanceAddiction.WebForms.Default" %>
<!doctype html>
<html lang="ko">
<head runat="server">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>강화중독</title>
  <link rel="stylesheet" href="Content/site.css" />
</head>
<body>
  <main class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">ENHANCE ADDICTION</p>
        <h1>강화중독</h1>
      </div>
    </header>

    <section class="login-card" id="login-panel" hidden>
      <h2>카카오 계정으로 시작하기</h2>
      <p>강화도, 골드, 사냥 시간은 서버에 저장됩니다. 로그아웃한 뒤에도 자동 사냥 시간은 정상적으로 누적됩니다.</p>
      <a class="login-button kakao" href="Auth/KakaoLogin.ashx">카카오로 로그인</a>
      <a class="login-button development" id="development-login" href="Auth/DevelopmentLogin.ashx" hidden>로컬 테스트 계정으로 시작</a>
    </section>

    <section class="account" id="nickname-panel" hidden>
      <h2>닉네임 설정</h2>
      <p>랭킹에 표시할 닉네임을 정해주세요. 한글, 영문, 숫자, 밑줄을 사용할 수 있습니다.</p>
      <form id="nickname-form">
        <input id="nickname-input" maxlength="12" autocomplete="off" placeholder="2~12자 닉네임" required />
        <button type="submit">저장</button>
      </form>
    </section>

    <section id="game" hidden>
      <div class="player-row">
        <p class="player-name" id="player-name"></p>
        <a class="logout" id="admin-link" href="Operator.ashx" hidden>관리자</a>
        <a class="logout" href="Auth/Logout.ashx">로그아웃</a>
      </div>
      <section class="level-card">
        <div class="level-header">
          <strong id="level">Lv. 1</strong>
          <span id="exp-text">0 / 100 EXP</span>
        </div>
        <div class="exp-track">
          <div class="exp-fill" id="exp-fill"></div>
        </div>
        <small id="stat-points">사용 가능한 스탯 포인트 0</small>
      </section>

      <div class="status-grid">
        <article>
          <span>보유 골드</span>
          <strong id="gold">0</strong>
        </article>
        <article>
          <span>현재 무기</span>
          <strong id="weapon">+0 검</strong>
        </article>
        <article>
          <span>공격력</span>
          <strong id="attack">10</strong>
        </article>
        <article>
          <span>보호권</span>
          <strong id="tickets">0장</strong>
        </article>
      </div>

      <nav class="tabs game-tabs">
        <button class="tab active" data-tab="hunt">사냥</button>
        <button class="tab" data-tab="enhance">강화</button>
        <button class="tab" data-tab="boss">보스</button>
        <button class="tab" data-tab="stats">스탯</button>
        <button class="tab" data-tab="rates">확률표</button>
        <button class="tab" data-tab="ranking">랭킹</button>
        <button class="tab" data-tab="collection" id="collection-tab" hidden>도감</button>
        <button class="tab" data-tab="guide">게임 안내</button>
      </nav>

      <section class="panel active" id="hunt-panel">
        <div class="section-title">
          <h2>자동 사냥</h2>
          <span>매일 한국 시간 자정 초기화</span>
        </div>
        <p class="budget-line" id="hunt-budget"></p>
        <div id="hunt-running" class="hunt-running"></div>
        <article class="manual-hunt-card">
          <div>
            <h3>직접 사냥</h3>
            <label class="manual-area-label">
              사냥터
              <select id="manual-hunt-area"></select>
            </label>
            <p id="manual-hunt-details"></p>
            <span id="manual-hunt-cooldown"></span>
          </div>
          <button class="manual-hunt-button" id="manual-hunt-button">몬스터 처치</button>
        </article>
        <div id="areas" class="card-list"></div>
      </section>

      <section class="panel" id="enhance-panel">
        <div class="section-title">
          <h2>무기 강화</h2>
          <span>파괴 시 +12 복구</span>
        </div>
        <article class="forge-card">
          <div class="weapon-orb" id="weapon-orb">+0</div>
          <div id="enhance-details"></div>
          <label class="ticket-option"><input type="checkbox" id="use-ticket" /> 파괴 시 보호권 사용</label>
          <button class="primary wide" id="enhance-button">강화하기</button>
        </article>
      </section>

      <section class="panel" id="boss-panel">
        <div class="section-title">
          <h2>관문 보스</h2>
          <span>처치 시 다음 지역 영구 해금</span>
        </div>
        <article class="boss-card" id="boss-card"></article>
      </section>

      <section class="panel" id="stats-panel">
        <div class="section-title">
          <h2>스탯</h2>
          <span id="available-points">남은 포인트 0</span>
        </div>
        <div id="stats-list" class="stats-list"></div>
        <button class="ghost wide" id="reset-stats">스탯 초기화</button>
      </section>

      <section class="panel" id="rates-panel">
        <div class="section-title">
          <h2>기본 강화 확률</h2>
          <span>장인의 손길 적용 전</span>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>시도</th><th>비용</th><th>성공</th><th>유지</th><th>파괴</th></tr>
            </thead>
            <tbody id="rates-body"></tbody>
          </table>
        </div>
      </section>

      <section class="panel" id="ranking-panel">
        <div class="section-title">
          <h2>실시간 랭킹</h2>
          <span>레벨 우선 · 현재 강화도 순</span>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>순위</th><th>닉네임</th><th>레벨</th><th>현재 강화</th><th>최고 강화</th></tr>
            </thead>
            <tbody id="ranking-body"></tbody>
          </table>
        </div>
      </section>

      <section class="panel" id="collection-panel">
        <div class="section-title">
          <h2>몬스터 도감</h2>
          <span id="collection-progress">0 / 360</span>
        </div>
        <p class="collection-description">
          직접 사냥에서 매우 낮은 확률로 등록됩니다. 같은 몬스터가 중복 등록될 수 있습니다.
        </p>
        <div class="collection-area-tabs" id="collection-area-tabs"></div>
        <div class="collection-grid" id="collection-grid"></div>
      </section>

      <section class="panel guide" id="guide-panel">
        <div class="section-title">
          <h2>게임 안내</h2>
          <span>모든 핵심 수치 공개</span>
        </div>
        <article class="guide-block">
          <h3>목표</h3>
          <p>사냥으로 골드와 경험치를 모으고 하나뿐인 검을 강화하세요. 관문 보스를 처치하면 다음 사냥터가 영구 해금됩니다. 최고 강화 단계는 <strong>+30</strong>입니다.</p>
        </article>
        <article class="guide-block">
          <h3>자동 사냥</h3>
          <ul>
            <li>브라우저를 닫아도 DB에 시작 시간이 기록됩니다.</li>
            <li>한국 시간 매일 오전 0시에 일일 시간이 초기화됩니다.</li>
            <li>기본 일일 자동 사냥 시간은 6시간입니다.</li>
            <li>관문 보스를 1마리 처치할 때마다 일일 시간이 30분 증가합니다.</li>
          </ul>
        </article>
        <article class="guide-block">
          <h3>직접 사냥</h3>
          <p>
            1초마다 1회 토벌할 수 있습니다. 골드는 자동 사냥의 평균 <strong>450%</strong>,
            경험치는 평균 <strong>375%</strong> 효율입니다.
            직접 사냥을 누르면 진행 중인 자동 사냥은 먼저 정산됩니다.
          </p>
        </article>
        <article class="guide-block" id="collection-guide" hidden>
          <h3>몬스터 등장과 도감 등록 확률</h3>
          <div class="table-wrap">
            <table>
              <thead>
                <tr><th>등급</th><th>등장 확률</th><th>처치 시 도감 등록 확률</th></tr>
              </thead>
              <tbody id="guide-monster-rates"></tbody>
            </table>
          </div>
          <p class="guide-note">등록 판정에 성공하면 해당 사냥터와 등급의 10종 중 하나가 무작위로 선택됩니다. 이미 등록된 몬스터도 다시 나올 수 있습니다.</p>
        </article>
        <article class="guide-block">
          <h3>강화</h3>
          <ul>
            <li>실패 시 무기는 유지됩니다.</li>
            <li>+15부터 파괴 확률이 생깁니다.</li>
            <li>파괴된 무기는 +12로 복구됩니다.</li>
            <li>보호권은 파괴 판정이 발생할 때만 소모됩니다.</li>
          </ul>
        </article>
        <article class="guide-block">
          <h3>사냥터와 보스</h3>
          <p>사냥터는 총 <strong>12개</strong>, 관문 보스는 총 <strong>11마리</strong>입니다.</p>
          <div class="table-wrap">
            <table>
              <thead>
                <tr><th>사냥터</th><th>입장</th><th>시간당 골드</th><th>시간당 경험치</th><th>다음 보스</th></tr>
              </thead>
              <tbody id="guide-areas"></tbody>
            </table>
          </div>
        </article>
      </section>

      <section class="log">
        <h2>최근 기록</h2>
        <ul id="messages"></ul>
      </section>
    </section>
  </main>
  <div class="toast" id="toast"></div>
  <script src="Scripts/game.js?v=20260603-1"></script>
</body>
</html>
