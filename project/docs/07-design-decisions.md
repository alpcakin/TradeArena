# 07 Design decisions

## The four main decisions

| # | Decision | Why | Price paid |
|---|---|---|---|
| 1 | **Four services, each with one job**: Market, Portfolio, Leaderboard, Game | Each has a different kind of data: the market stores nothing, the Portfolio Service owns the money, the leaderboard is only a calculation, the Game Service owns time and history. Every piece of data has exactly one service that writes it ([data ownership](02-architecture.md#data-ownership)). | More processes to run and a broker in the middle; the leaderboard can be up to one second behind a trade |
| 2 | **Messages instead of calls between services** | No service waits for another one, and a stopped service does not break the others. New listeners can be added without touching the sender: the Leaderboard Service was added without changing the Market Service. The Portfolio Service even keeps its own copy of the prices, so it never has to ask the Market Service. | An order's result arrives a moment later as a message, not as the answer to the click |
| 3 | **Orders are handled one at a time** in the Portfolio Service (the Leaderboard Service works the same way) | Two orders of the same player can never spend the same cash at once, and a new round is always handled before the orders after it. No locks are needed, and one message at a time is far more than the game needs. | These two services cannot be sped up just by starting more copies |
| 4 | **One-click sign-in with a nickname**, no accounts or passwords | The point of the project is the realtime, message-driven game, not user management. One click is enough to play, and a page reload keeps the player. | Whoever has the player id is that player; nicknames are not unique |

## Course concepts used

| Concept | Where |
|---|---|
| Microservices with their own data (database per service) | `tradearena_portfolio`, `tradearena_game`; no service reads another's database |
| Publish/subscribe | Every interested service gets its own copy of a message ([03](03-communication.md#how-rabbitmq-delivers-the-messages)) |
| Command vs event | `PlaceOrder` goes to one queue and can be rejected; everything else is a published event |
| Read model built from events | `Standings` in the Leaderboard Service |
| Eventual consistency | The ranking follows a trade within one tick; the browser shows results when the events arrive |
| At-least-once delivery, idempotent consumer | `OrderId` as the key of the `Trades` table ([03](03-communication.md#when-something-goes-wrong)) |
| API gateway as single entry point | `src/Gateway`: web client, SignalR hub, forwarding to the Game Service |
| Real-time push over WebSocket | SignalR hub `/hubs/arena`, one group per player, automatic reconnect |
| Containerised deployment | One Dockerfile per service, Docker Compose with health checks |

## Known limitations

- **No real authentication** (decision 4). Enough for a classroom game, not for real money.
- **Nicknames are not unique.** Two players called `alex` both appear on the leaderboard.
- **Leaderboard restart.** After a restart, the Leaderboard Service only knows the players who join or trade again.
- **Market restart.** The Market Service does not store prices; after a restart they begin again from the start values.
