# 05 API reference

The browser uses two interfaces of the API Gateway: the SignalR hub for everything live, and two HTTP endpoints for the round state when the page opens. All JSON uses camelCase property names, and enums are sent as strings (`"Buy"`, `"Sell"`).

## SignalR hub: `/hubs/arena`

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/arena")
  .withAutomaticReconnect()
  .build();

connection.on("marketTick", tick => console.log(tick.prices));
await connection.start();

const playerId = await connection.invoke("Join", "alice", null);
await connection.invoke("PlaceOrder", "TECH", "Buy", 10);
```

### Methods (browser → server)

| Method | Parameters | Returns | Errors |
|---|---|---|---|
| `Join` | `nickname: string`, `playerId: Guid \| null` | `Guid`: the player id to remember | `HubException` if the nickname is not 2–16 characters |
| `PlaceOrder` | `symbol: string`, `side: "Buy" \| "Sell"`, `quantity: int` | `Guid`: the order id | `HubException` if the connection has not joined or the quantity is outside 1–10,000 |

`Join` with `null` creates a new player; `Join` with a saved id continues that player. Calling it again on the same connection is allowed and is how the browser enters a new round. It publishes `PlayerJoined`.

`PlaceOrder` only confirms that the order was handed over. The result arrives later as `orderExecuted` or `orderRejected`.

### Events (server → browser)

| Event | Sent to | Payload |
|---|---|---|
| `marketTick` | everyone | `MarketTick` |
| `leaderboardUpdated` | everyone | `LeaderboardUpdated` |
| `roundStarted` | everyone | `RoundStarted` |
| `roundEnded` | everyone | `RoundEnded` |
| `portfolioUpdated` | the player only | `PortfolioUpdated` |
| `orderExecuted` | the player only | `OrderExecuted` |
| `orderRejected` | the player only | `OrderRejected` |

Examples:

```jsonc
// marketTick
{
  "prices": [
    { "symbol": "TECH", "name": "TechCorp", "price": 121.37, "changePercent": 1.14 },
    { "symbol": "FOOD", "name": "Foodies Inc", "price": 44.82, "changePercent": -0.4 }
  ],
  "timestamp": "2026-11-25T22:46:41.0034Z"
}

// portfolioUpdated
{
  "playerId": "3f6c0f4e-6a4e-4a39-9a0a-2a7d2c6a6b1e",
  "nickname": "alice",
  "roundNumber": 2,
  "cash": 8898.30,
  "holdings": [ { "symbol": "TECH", "quantity": 10 } ]
}

// orderExecuted
{
  "orderId": "b0d1c7a2-…", "playerId": "3f6c0f4e-…",
  "symbol": "TECH", "side": "Buy", "quantity": 10, "price": 110.17, "total": 1101.70
}

// orderRejected
{
  "orderId": "c41e…", "playerId": "3f6c0f4e-…",
  "symbol": "FOOD", "side": "Sell", "quantity": 10,
  "reason": "Not enough shares: you own 0 FOOD."
}

// leaderboardUpdated
{
  "roundNumber": 2,
  "entries": [
    { "rank": 1, "nickname": "bob", "netWorth": 10021.20, "profitPercent": 0.21 },
    { "rank": 2, "nickname": "alice", "netWorth": 9967.05, "profitPercent": -0.33 }
  ],
  "timestamp": "2026-11-25T22:47:31.0021Z"
}

// roundStarted
{ "roundNumber": 3, "startedAt": "2026-11-25T22:47:42.598Z", "endsAt": "2026-11-25T22:52:42.598Z" }

// roundEnded
{
  "roundNumber": 3,
  "endedAt": "2026-11-25T22:52:42.608Z",
  "nextRoundAt": "2026-11-25T22:53:02.608Z",
  "finalStandings": [ { "rank": 1, "nickname": "bob", "netWorth": 10021.20, "profitPercent": 0.21 } ]
}
```

### Rejection reasons

| Reason | When |
|---|---|
| `Join the arena first.` | The player id is unknown to the Portfolio Service |
| `The round is over. Wait for the next one to start.` | The order arrived during the break |
| `The market has not opened yet.` | The Portfolio Service has not received a price yet |
| `Unknown stock: XYZ.` | The symbol is not one of the five stocks |
| `Not enough cash: 100 TECH costs $12,000.00, you have $10,000.00.` | Buy order larger than the cash |
| `Not enough shares: you own 3 BANK.` | Sell order larger than the shares owned |
| `Quantity must be at least 1.` | Quantity below 1 |

## HTTP endpoints of the Gateway

### `GET /api/rounds/current`

The round that is running now, or the break after it.

```json
{
  "number": 5,
  "phase": "Running",
  "startedAt": "2026-11-25T22:49:58.322Z",
  "endsAt": "2026-11-25T22:54:58.322Z",
  "nextRoundAt": null
}
```

`phase` is `Running` or `Break`; during the break `nextRoundAt` is set. Status codes: `200`, `404` before the first round has started, `503` if the Game Service cannot be reached.

### `GET /api/rounds`

The last ten finished rounds in which somebody traded (at least one net worth differs from $10,000), newest first, with the top three of each.

```json
[
  {
    "number": 3,
    "startedAt": "2026-11-25T22:47:42.598Z",
    "endedAt": "2026-11-25T22:48:42.608Z",
    "players": 2,
    "top": [
      { "rank": 1, "nickname": "alp", "netWorth": 10000.0, "profitPercent": 0.0 },
      { "rank": 2, "nickname": "bob", "netWorth": 9965.0, "profitPercent": -0.35 }
    ]
  }
]
```

Status codes: `200`, `503` if the Game Service cannot be reached.

## Internal endpoints of the Game Service

The Gateway forwards the two requests above to the Game Service. These endpoints are only reachable inside the Docker network (or on `http://localhost:5103` when started with `dotnet run`).

| Endpoint | Description |
|---|---|
| `GET /rounds/current` | Same body as `/api/rounds/current` |
| `GET /rounds?take=N` | Finished rounds in which somebody traded, `N` between 1 and 50 (default 10) |

The other services have no HTTP interface; they are reached only through RabbitMQ.
