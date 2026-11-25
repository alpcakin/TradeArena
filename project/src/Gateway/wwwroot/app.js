"use strict";

const STARTING_CASH = 10000;
const STORAGE_KEY = "tradearena.player";

const $ = id => document.getElementById(id);
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const percent = value => (value >= 0 ? "+" : "") + value.toFixed(2) + "%";

const state = {
  player: loadPlayer(),   // { id, nickname }, kept in the browser so a reload keeps the same player
  joined: false,
  connected: false,
  prices: {},             // symbol -> last StockPrice
  rows: {},               // symbol -> <tr> of the market table
  portfolio: null,        // last PortfolioUpdated
  round: null,            // { number, phase, endsAt, nextRoundAt }
  clockOffset: 0,         // server time minus browser time, in ms
};

function loadPlayer() {
  try { return JSON.parse(localStorage.getItem(STORAGE_KEY)); } catch { return null; }
}

function savePlayer(player) {
  try {
    if (player) localStorage.setItem(STORAGE_KEY, JSON.stringify(player));
    else localStorage.removeItem(STORAGE_KEY);
  } catch { /* private mode: the player is simply not remembered */ }
}

// SignalR wraps server errors as "... HubException: <message>". Only the message is shown.
function errorText(err) {
  const text = String(err?.message ?? err);
  const marker = "HubException: ";
  return text.includes(marker) ? text.slice(text.indexOf(marker) + marker.length) : text;
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/arena")
  .withAutomaticReconnect()
  .build();

// ---------- market ----------

connection.on("marketTick", tick => {
  state.clockOffset = new Date(tick.timestamp).getTime() - Date.now();
  for (const stock of tick.prices) renderPrice(stock);
  renderPortfolio();
});

function renderPrice(stock) {
  let row = state.rows[stock.symbol];
  if (!row) {
    $("prices").querySelector("tr.empty")?.remove();
    row = el("tr");
    const name = el("td", "stock");
    name.append(el("strong", "", stock.symbol), el("small", "", stock.name));
    const actions = el("td", "actions");
    for (const side of ["Buy", "Sell"]) {
      const button = el("button", side.toLowerCase(), side);
      button.type = "button";
      button.addEventListener("click", () => placeOrder(stock.symbol, side));
      actions.append(button);
    }
    row.append(name, el("td", "num price"), el("td", "num change"), el("td", "num owned", "0"), actions);
    $("prices").append(row);
    state.rows[stock.symbol] = row;
    updateButtons();
  }

  const previous = state.prices[stock.symbol]?.price;
  const priceCell = row.querySelector(".price");
  const changeCell = row.querySelector(".change");
  priceCell.textContent = money.format(stock.price);
  changeCell.textContent = percent(stock.changePercent);
  changeCell.className = "num change " + (stock.changePercent >= 0 ? "up" : "down");

  if (previous !== undefined && previous !== stock.price) {
    priceCell.classList.remove("flash-up", "flash-down");
    void priceCell.offsetWidth; // restart the animation
    priceCell.classList.add(stock.price > previous ? "flash-up" : "flash-down");
  }
  state.prices[stock.symbol] = stock;
}

// ---------- joining ----------

async function join(nickname) {
  const id = await connection.invoke("Join", nickname, state.player?.id ?? null);
  state.player = { id, nickname: nickname.trim() };
  state.joined = true;
  savePlayer(state.player);

  $("join").hidden = true;
  $("portfolio").hidden = false;
  $("me").hidden = false;
  $("me-name").textContent = state.player.nickname;
  updateButtons();
}

$("join").addEventListener("submit", async event => {
  event.preventDefault();
  $("join-error").textContent = "";
  try {
    await join($("nickname").value);
    log(`Welcome, ${state.player.nickname}!`);
  } catch (err) {
    $("join-error").textContent = errorText(err);
  }
});

$("leave").addEventListener("click", () => {
  savePlayer(null);
  location.reload();
});

// ---------- orders and portfolio ----------

async function placeOrder(symbol, side) {
  const quantity = parseInt($("qty").value, 10);
  if (!(quantity >= 1)) {
    log("Enter a quantity of at least 1.", "error");
    return;
  }
  try {
    await connection.invoke("PlaceOrder", symbol, side, quantity);
  } catch (err) {
    log(errorText(err), "error");
  }
}

connection.on("orderExecuted", o => {
  const verb = o.side === "Buy" ? "Bought" : "Sold";
  log(`${verb} ${o.quantity} ${o.symbol} at ${money.format(o.price)} (${money.format(o.total)})`, "ok");
});

connection.on("orderRejected", o => {
  log(`${o.side} ${o.quantity} ${o.symbol} rejected: ${o.reason}`, "error");
});

connection.on("portfolioUpdated", portfolio => {
  state.portfolio = portfolio;
  renderPortfolio();
});

function renderPortfolio() {
  const p = state.portfolio;
  if (!p) return;

  const owned = Object.fromEntries(p.holdings.map(h => [h.symbol, h.quantity]));
  for (const [symbol, row] of Object.entries(state.rows)) {
    row.querySelector(".owned").textContent = owned[symbol] ?? 0;
  }

  const shares = p.holdings.reduce((sum, h) => sum + h.quantity * (state.prices[h.symbol]?.price ?? 0), 0);
  const netWorth = p.cash + shares;
  const profit = (netWorth - STARTING_CASH) / STARTING_CASH * 100;

  $("cash").textContent = money.format(p.cash);
  $("shares").textContent = money.format(shares);
  $("networth").textContent = money.format(netWorth);
  $("profit").textContent = percent(profit);
  $("profit").className = profit >= 0 ? "up" : "down";
}

function updateButtons() {
  const canTrade = state.joined && state.connected && state.round?.phase !== "Break";
  for (const button of document.querySelectorAll(".actions button")) {
    button.disabled = !canTrade;
    button.title = canTrade ? "" : state.joined ? "Wait for the next round" : "Join the arena to trade";
  }
}

// ---------- leaderboard ----------

connection.on("leaderboardUpdated", board => {
  // During the break the final ranking of the finished round stays on screen.
  if (state.round?.phase === "Break") return;
  renderRanking($("leaderboard"), board.entries, 10, "No players in this round yet.");
});

// Shows the top entries; if the current player is further down, their own line is added below.
function renderRanking(list, entries, top, emptyText) {
  list.replaceChildren();
  if (!entries.length) {
    list.append(el("li", "empty", emptyText));
    return;
  }

  const isMe = entry => state.joined && entry.nickname === state.player.nickname;
  const rows = entries.slice(0, top);
  const mine = entries.find(isMe);
  if (mine && !rows.includes(mine)) rows.push(null, mine);

  for (const entry of rows) {
    if (!entry) {
      list.append(el("li", "gap", "⋯"));
      continue;
    }
    const li = el("li", isMe(entry) ? "me" : "");
    li.append(
      el("span", "rank", `#${entry.rank}`),
      el("span", "name", entry.nickname),
      el("span", "value", money.format(entry.netWorth)),
      el("span", "profit " + (entry.profitPercent >= 0 ? "up" : "down"), percent(entry.profitPercent)),
    );
    list.append(li);
  }
}

// ---------- rounds ----------

connection.on("roundStarted", r => {
  state.round = { number: r.roundNumber, phase: "Running", endsAt: r.endsAt };
  if ($("round-over").open) $("round-over").close();
  log(`Round ${r.roundNumber} started. Everyone is back to ${money.format(STARTING_CASH)}.`);
  updateButtons();

  // Enter the new round: the Portfolio Service resets our money and the leaderboard lists us again.
  if (state.joined) join(state.player.nickname).catch(err => log(errorText(err), "error"));
});

connection.on("roundEnded", r => {
  state.round = { number: r.roundNumber, phase: "Break", nextRoundAt: r.nextRoundAt };
  updateButtons();
  renderRanking($("leaderboard"), r.finalStandings, 10, "Nobody traded in this round.");
  showRoundOver(r);
  loadHistory();
});

function showRoundOver(r) {
  const winner = r.finalStandings[0];
  $("ro-title").textContent = `Round ${r.roundNumber} is over`;
  $("ro-winner").textContent = winner
    ? `🏆 ${winner.nickname} wins with ${money.format(winner.netWorth)} (${percent(winner.profitPercent)})`
    : "Nobody traded in this round.";
  renderRanking($("ro-standings"), r.finalStandings, 5, "No players.");

  const mine = state.joined && r.finalStandings.find(e => e.nickname === state.player.nickname);
  $("ro-me").textContent = mine ? `You finished #${mine.rank} of ${r.finalStandings.length}.` : "";
  log(winner ? `Round ${r.roundNumber} is over. Winner: ${winner.nickname}.` : `Round ${r.roundNumber} is over.`);

  if (!$("round-over").open) $("round-over").showModal();
}

function formatDuration(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, "0")}`;
}

function renderClock() {
  const r = state.round;
  const now = Date.now() + state.clockOffset;
  let text;
  if (!state.connected) text = "Connecting…";
  else if (!r) text = "Waiting for the first round…";
  else if (r.phase === "Running") text = `Round ${r.number} · ${formatDuration(new Date(r.endsAt) - now)} left`;
  else text = `Round ${r.number} finished · next round in ${formatDuration(new Date(r.nextRoundAt) - now)}`;

  $("round").textContent = text;
  if (r?.phase === "Break") $("ro-next").textContent = formatDuration(new Date(r.nextRoundAt) - now);
}

setInterval(renderClock, 250);

async function loadRound() {
  try {
    const response = await fetch("/api/rounds/current");
    if (!response.ok) return;
    const s = await response.json();
    state.round = { number: s.number, phase: s.phase, endsAt: s.endsAt, nextRoundAt: s.nextRoundAt };
    updateButtons();
  } catch { /* the round timer still arrives through SignalR */ }
}

async function loadHistory() {
  try {
    const response = await fetch("/api/rounds");
    if (!response.ok) return;
    const rounds = await response.json();
    const list = $("history");
    list.replaceChildren();
    if (!rounds.length) {
      list.append(el("li", "empty", "No finished rounds yet."));
      return;
    }
    for (const round of rounds) {
      const winner = round.top[0];
      const players = round.players === 1 ? "1 player" : `${round.players} players`;
      list.append(el("li", "", `Round ${round.number} · 🏆 ${winner.nickname} ${money.format(winner.netWorth)} (${percent(winner.profitPercent)}) · ${players}`));
    }
  } catch { /* history is optional */ }
}

// ---------- activity log ----------

function log(text, kind = "info") {
  const list = $("activity");
  list.querySelector(".empty")?.remove();
  const li = el("li", kind);
  li.append(el("time", "", new Date().toLocaleTimeString()), document.createTextNode(text));
  list.prepend(li);
  while (list.children.length > 30) list.lastChild.remove();
}

// ---------- connection ----------

connection.onreconnecting(() => {
  state.connected = false;
  updateButtons();
});

connection.onreconnected(async () => {
  state.connected = true;
  updateButtons();
  await loadRound();
  // A new connection has no player attached yet, so join again.
  if (state.joined) await join(state.player.nickname).catch(err => log(errorText(err), "error"));
});

connection.onclose(() => {
  state.connected = false;
  updateButtons();
  setTimeout(start, 3000);
});

async function start() {
  try {
    await connection.start();
  } catch {
    setTimeout(start, 3000);
    return;
  }
  state.connected = true;
  await Promise.all([loadRound(), loadHistory()]);
  updateButtons();

  if (state.player) {
    $("nickname").value = state.player.nickname;
    try {
      await join(state.player.nickname);
    } catch (err) {
      $("join-error").textContent = errorText(err);
    }
  }
}

renderRanking($("leaderboard"), [], 10, "No players in this round yet.");
start();
