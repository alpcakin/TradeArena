# TradeArena documentation

TradeArena is a realtime multiplayer stock trading game made of four C#/.NET microservices and an API gateway. The services talk to each other through RabbitMQ, and the browser receives every change over a WebSocket (SignalR).

| # | Page | Answers |
|---|---|---|
| 01 | [Overview](01-overview.md) | What the game is, the rules, the course requirements and the services |
| 02 | [Architecture](02-architecture.md) | How the parts fit together, who owns which data, how it is deployed |
| 03 | [Communication](03-communication.md) | Which messages exist and what happens, step by step, on every player action |
| 04 | [Data model](04-data-model.md) | Database tables, in-memory state and the message contracts |
| 05 | [API reference](05-api-reference.md) | SignalR hub methods and events, HTTP endpoints, rejection reasons |
| 06 | [Running and testing](06-running-and-testing.md) | How to start it, configure it, test it and demo it |
| 07 | [Design decisions](07-design-decisions.md) | Why it is built this way, the course concepts it uses and its limits |

All diagrams are Mermaid code blocks, so GitHub renders them directly and they can be edited as text.
