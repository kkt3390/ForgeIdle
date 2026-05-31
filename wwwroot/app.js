let state;
let catalog;
let manualHuntAvailableAt;
const $ = selector => document.querySelector(selector);
const number = value => Number(value).toLocaleString("ko-KR");
const percent = value => `${(value * 100).toLocaleString("ko-KR", { maximumFractionDigits: 2 })}%`;

async function api(path, body) {
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.message || "요청을 처리하지 못했습니다.");
  return result;
}

async function loadSession() {
  const response = await fetch("/api/auth/me");
  if (!response.ok) {
    $("#login-panel").hidden = false;
    $("#nickname-panel").hidden = true;
    $("#game").hidden = true;
    $("#change-account").hidden = true;
    return;
  }
  state = await response.json();
  $("#login-panel").hidden = true;
  $("#change-account").hidden = false;
  $("#nickname-panel").hidden = Boolean(state.nickname);
  $("#game").hidden = !state.nickname;
  render();
}

async function action(path, body) {
  try {
    const result = await api(`/api/game${path}`, body);
    state = result.state;
    manualHuntAvailableAt = result.manualHuntAvailableAt || state.manualHunt.availableAt;
    toast(result.message);
    render();
  } catch (error) { toast(error.message); }
}

function render() {
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
  renderHunt(); renderEnhance(); renderBoss(); renderStats();
  $("#messages").innerHTML = state.recentMessages.map(x => `<li>${escapeHtml(x)}</li>`).join("");
}

async function loadRankings() {
  const response = await fetch("/api/rankings");
  const rankings = await response.json();
  $("#ranking-body").innerHTML = rankings.map(row => `<tr>
    <td>${row.rank}</td><td>${escapeHtml(row.nickname)}</td><td>Lv. ${row.level}</td>
    <td>+${row.weaponLevel}</td><td>+${row.highestWeaponLevel}</td>
  </tr>`).join("");
}

function renderHunt() {
  $("#hunt-running").innerHTML = state.hunt ? `<div class="running-card"><div><strong>${state.hunt.areaName} 자동 사냥 중</strong><span id="hunt-timer"></span><span id="hunt-remaining"></span></div><button onclick="action('/hunt/claim')">종료 및 정산</button></div>` : "";
  updateAutomaticHuntBudget();
  updateTimer();
  $("#manual-hunt-details").textContent = `${state.manualHunt.areaName} 기준 · 평균 시간당 ${number(state.manualHunt.automaticGoldPerHour * 1.5)} 골드 · 경험치 ${number(state.manualHunt.automaticExperiencePerHour * 1.25)}`;
  updateManualHuntButton();
  $("#areas").innerHTML = state.availableAreas.map(area => `
    <article class="area-card"><h3>${area.name}</h3>
      <p>시간당 ${number(area.goldPerHour)} 골드 · 경험치 ${number(area.experiencePerHour)} · 입장 +${area.requiredEnhancement}</p>
      <button ${!area.canEnter || state.hunt ? "disabled" : ""} onclick="action('/hunt/start', { areaId: ${area.id} })">사냥 시작</button>
    </article>`).join("");
}

function renderEnhance() {
  const rule = state.currentEnhancement;
  if (!rule) { $("#enhance-details").innerHTML = "<strong>최고 강화 단계입니다.</strong>"; $("#enhance-button").disabled = true; return; }
  const base = catalog.enhancements[rule.currentLevel];
  $("#enhance-details").innerHTML = `<strong>+${rule.currentLevel} → +${rule.currentLevel + 1}</strong>
    <p>비용 ${number(rule.cost)} 골드</p>
    <p>성공 ${percent(rule.successRate)} · 유지 ${percent(rule.keepRate)} · 파괴 ${percent(rule.destroyRate)}</p>
    ${base.successRate !== rule.successRate ? `<small>장인의 손길 적용 전 성공률 ${percent(base.successRate)}</small>` : ""}`;
  $("#enhance-button").disabled = Boolean(state.hunt) || state.gold < rule.cost;
  $("#use-ticket").disabled = rule.destroyRate === 0;
}

function renderBoss() {
  const boss = state.nextBoss;
  $("#boss-card").innerHTML = !boss ? "<h3>모든 관문 보스를 처치했습니다.</h3><p>최고 강화를 향해 나아가세요.</p>" :
    `<h3>${boss.name}</h3><p>체력 <strong>${number(boss.health)}</strong></p><p>도전 조건 <strong>무기 +${boss.requiredEnhancement}</strong></p><button ${!boss.canChallenge || state.hunt ? "disabled" : ""} onclick="action('/boss')">보스 도전</button>`;
}

function renderStats() {
  $("#available-points").textContent = `남은 포인트 ${state.availableStatPoints}`;
  const rows = [
    ["dualWield", "이도류", state.stats.dualWield, "직접 사냥 추가 토벌 확률", .5, "%"],
    ["goldGain", "노련한 사냥꾼", state.stats.goldGain, "모든 사냥 골드 획득량", 1, "%"],
    ["experienceGain", "성장의 축복", state.stats.experienceGain, "모든 사냥 경험치 획득량", 1, "%"],
    ["artisanTouch", "장인의 손길", state.stats.artisanTouch, "강화 성공률 상대 보정", .5, "%"]
  ];
  $("#stats-list").innerHTML = rows.map(([key, name, value, description, perPoint, unit]) => `
    <article class="stat-card"><div><h3>${name} <small>Lv. ${value}/20</small></h3>
      <p>${description} +${value * perPoint}${unit}</p></div>
      <button ${state.availableStatPoints <= 0 || value >= 20 ? "disabled" : ""} onclick="action('/stats/invest', { stat: '${key}' })">+1</button>
    </article>`).join("");
  $("#reset-stats").textContent = `스탯 초기화 · ${number(state.statResetCost)} 골드`;
}

function renderRates() {
  $("#rates-body").innerHTML = catalog.enhancements.map(rule => `<tr><td>+${rule.currentLevel} → +${rule.currentLevel + 1}</td><td>${number(rule.cost)}</td><td>${percent(rule.successRate)}</td><td>${percent(rule.keepRate)}</td><td>${percent(rule.destroyRate)}</td></tr>`).join("");
}

function renderGuide() {
  $("#guide-areas").innerHTML = catalog.areas.map(area => `<tr>
    <td>${area.name}</td><td>+${area.requiredEnhancement}</td><td>${number(area.goldPerHour)}</td>
    <td>${number(area.experiencePerHour)}</td><td>${area.bossRequiredEnhancement === null ? "최종 지역" : `+${area.bossRequiredEnhancement}`}</td>
  </tr>`).join("");
}

function updateTimer() {
  if (!state?.hunt || !$("#hunt-timer")) return;
  const elapsed = Math.min(Date.now() - new Date(state.hunt.startedAt).getTime(), new Date(state.hunt.rewardCapAt).getTime() - new Date(state.hunt.startedAt).getTime());
  const remaining = Math.max(0, new Date(state.hunt.rewardCapAt).getTime() - Date.now());
  $("#hunt-timer").textContent = `${formatDuration(Math.floor(elapsed / 1000))} 누적`;
  $("#hunt-remaining").textContent = remaining > 0 ? `보상 누적 가능 ${formatDuration(Math.ceil(remaining / 1000))} 남음` : "자동 사냥 보상이 가득 찼습니다.";
  updateAutomaticHuntBudget();
}

function updateAutomaticHuntBudget() {
  if (!state) return;
  let remainingSeconds = Math.floor(state.automaticHuntBudget.remainingHours * 3600);
  if (state.hunt) {
    const elapsed = Math.max(0, Math.min(Date.now(), new Date(state.hunt.rewardCapAt).getTime()) - new Date(state.hunt.startedAt).getTime());
    remainingSeconds = Math.max(0, remainingSeconds - Math.floor(elapsed / 1000));
  }
  $("#hunt-budget").textContent = `오늘 남은 자동 사냥 ${formatDuration(remainingSeconds)} / 총 ${formatDuration(Math.floor(state.automaticHuntBudget.limitHours * 3600))}`;
}

function updateManualHuntButton() {
  if (!state) return;
  const availableAt = manualHuntAvailableAt || state.manualHunt.availableAt;
  const remaining = availableAt ? Math.max(0, new Date(availableAt).getTime() - Date.now()) : 0;
  $("#manual-hunt-button").disabled = remaining > 0;
  $("#manual-hunt-button").textContent = remaining > 0 ? `${Math.ceil(remaining / 1000)}초 후 가능` : "몬스터 처치";
  $("#manual-hunt-cooldown").textContent = remaining > 0 ? "다음 몬스터를 찾고 있습니다." : state.hunt ? "클릭하면 자동 사냥을 정산하고 직접 사냥으로 전환합니다." : "3초마다 직접 처치할 수 있습니다.";
}

function formatDuration(seconds) { return `${Math.floor(seconds / 3600)}시간 ${Math.floor(seconds % 3600 / 60)}분 ${seconds % 60}초`; }
function toast(message) { $("#toast").textContent = message; $("#toast").classList.add("show"); setTimeout(() => $("#toast").classList.remove("show"), 2600); }
function escapeHtml(value) { const div = document.createElement("div"); div.textContent = value; return div.innerHTML; }

$("#change-account").addEventListener("click", async () => { await api("/api/auth/logout"); state = undefined; $("#login-panel").hidden = false; $("#nickname-panel").hidden = true; $("#game").hidden = true; $("#change-account").hidden = true; });
$("#nickname-form").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const response = await fetch("/api/auth/nickname", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ nickname: $("#nickname-input").value })
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.message);
    state = result;
    $("#nickname-panel").hidden = true;
    $("#game").hidden = false;
    render();
    toast("닉네임을 저장했습니다.");
  } catch (error) { toast(error.message); }
});
$("#enhance-button").addEventListener("click", () => action("/enhance", { useProtection: $("#use-ticket").checked }));
$("#manual-hunt-button").addEventListener("click", () => action("/hunt/manual"));
$("#reset-stats").addEventListener("click", () => action("/stats/reset"));
document.querySelectorAll(".tab").forEach(tab => tab.addEventListener("click", () => {
  document.querySelectorAll(".tab, .panel").forEach(x => x.classList.remove("active"));
  tab.classList.add("active"); $(`#${tab.dataset.tab}-panel`).classList.add("active");
  if (tab.dataset.tab === "ranking") loadRankings();
}));
setInterval(() => { updateTimer(); updateAutomaticHuntBudget(); updateManualHuntButton(); }, 250);
fetch("/api/catalog").then(x => x.json()).then(value => { catalog = value; renderRates(); renderGuide(); loadSession(); });
fetch("/api/auth/providers").then(x => x.json()).then(providers => {
  const unavailable = [];
  if (!providers.kakao) { $("#kakao-login").classList.add("disabled"); unavailable.push("카카오"); }
  $("#test-login").hidden = !providers.testLogin;
  $("#login-message").textContent = unavailable.length ? `${unavailable.join(", ")} 개발자 키를 설정하면 로그인을 사용할 수 있습니다.` : "원하는 로그인 방식을 선택하세요.";
});
const loginError = new URLSearchParams(location.search).get("loginError");
if (loginError) {
  history.replaceState({}, "", "/");
  setTimeout(() => toast("소셜 로그인에 실패했습니다. 잠시 후 다시 시도해 주세요."), 0);
}
