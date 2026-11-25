namespace TradeArena.Contracts;

// Messages are the "language" the services use to talk to each other through RabbitMQ.
// They live in a shared project so every service agrees on the same shape.

public record StockPrice(string Symbol, string Name, decimal Price, decimal ChangePercent);

// Published by the Market Service every second with the latest price of every stock.
public record MarketTick(StockPrice[] Prices, DateTime Timestamp);
