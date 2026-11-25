# 06 Running and testing

## Start everything with Docker

Requirement: Docker Desktop (or Docker Engine with Compose v2).

```bash
cd project
docker compose up --build
```

| Address | What |
|---|---|
| <http://localhost:8080> | The game |
| <http://localhost:15672> | RabbitMQ management UI (user `guest`, password `guest`) |
| `localhost:5432` | PostgreSQL (user `postgres`, password `postgres`) |

The first build downloads the .NET and infrastructure images and takes a few minutes. The first round starts about 5 seconds after the Game Service is up.

Shorter rounds are useful for a demo:

```bash
ROUND_SECONDS=60 BREAK_SECONDS=10 docker compose up --build
```

Stop with `Ctrl+C` or `docker compose down`. Players and round history are kept in the `pgdata` volume; `docker compose down -v` deletes them as well.

## Run the services from source

Useful while changing code. Start only the infrastructure in Docker, then each service with `dotnet run` (requires the .NET 10 SDK):

```bash
cd project
docker compose up rabbitmq postgres
```

```bash
dotnet run --project src/MarketService
dotnet run --project src/PortfolioService
dotnet run --project src/LeaderboardService
dotnet run --project src/GameService       # http://localhost:5103
dotnet run --project src/Gateway           # http://localhost:5074
```

Each command runs in its own terminal. The default settings in `appsettings.json` point to `localhost`, so no configuration is needed.

## Configuration

Settings come from `appsettings.json` and can be overridden with environment variables (`__` separates sections).

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `RabbitMq__Host` | all | `localhost` | RabbitMQ host name (`rabbitmq` in Compose) |
| `RabbitMq__Username` / `RabbitMq__Password` | all | `guest` / `guest` | RabbitMQ credentials |
| `ConnectionStrings__Portfolio` | Portfolio | `Host=localhost;Database=tradearena_portfolio;…` | PostgreSQL connection |
| `ConnectionStrings__Game` | Game | `Host=localhost;Database=tradearena_game;…` | PostgreSQL connection |
| `Game__RoundSeconds` | Game | `300` | Length of a round (`ROUND_SECONDS` in Compose) |
| `Game__BreakSeconds` | Game | `20` | Break between rounds (`BREAK_SECONDS` in Compose) |
| `Game__StartDelaySeconds` | Game | `5` | Wait before the first round |
| `Services__Game` | Gateway | `http://localhost:5103` | Base address of the Game Service (`http://game:8080` in Compose) |

## Automated tests

```bash
cd project
dotnet test
```

The test project `tests/TradeArena.Tests` contains 19 xUnit tests for the logic that decides the game. They need neither RabbitMQ nor a database, because the rules live in plain classes.

| Test class | What it checks |
|---|---|
| `PlayerTests` | Starting cash; buying takes cash and adds shares; buying too much is rejected without changes; spending exactly all the cash is allowed; selling at the current price; selling shares you do not own is rejected; quantity must be positive; a new round resets cash and shares, the same round does not |
| `RankingTests` | Net worth = cash + shares × latest price; sorting and profit in percent; equal net worth shares a rank; snapshots from an old round are ignored; a new round clears the standings |
| `StockTests` | Each price step stays within ±2 % and is rounded to cents; the price never drops below $1; the change is measured from the start price |

## Manual end-to-end check

These steps were used to check the running system and work well as a live demo (with `ROUND_SECONDS=60 BREAK_SECONDS=10`).

1. Open <http://localhost:8080>. Prices move every second and the top bar shows the round timer, before anyone has joined.
2. Join as `alice`. The portfolio shows $10,000 and `alice` appears on the leaderboard.
3. Buy 10 `TECH`. The activity list shows "Bought 10 TECH at …", cash goes down and the **Owned** column shows 10.
4. Sell 10 `FOOD` without owning any: "rejected: Not enough shares: you own 0 FOOD."
5. Set the quantity to 1000 and buy `TECH`: "rejected: Not enough cash …".
6. Open a second player in another browser, a private window or <http://127.0.0.1:8080> (a different origin has its own `localStorage`). Join as `bob` and buy something. Both players appear on both leaderboards, but each sees only their own orders.
7. Wait for the timer. A dialog shows the winner and the final ranking, the buy and sell buttons are disabled, and **Past rounds** lists the round.
8. After the break both players are back to $10,000 and on the new leaderboard.
9. Reload a page: the same player continues with the same shares.
10. Run `docker compose stop portfolio`, place an order, then `docker compose start portfolio`. The order waits in the queue and is confirmed a few seconds later.
11. Open <http://localhost:15672> → **Queues and Streams** to see the `portfolio`, `leaderboard`, `game` and temporary Gateway queues and their message rates.
