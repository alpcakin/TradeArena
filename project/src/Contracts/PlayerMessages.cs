namespace TradeArena.Contracts;

public enum OrderSide { Buy, Sell }

// Event published by the Gateway when a player enters the arena (first visit, page reload or new round).
public record PlayerJoined(Guid PlayerId, string Nickname);

// Command sent by the Gateway directly to the Portfolio Service queue. It can be rejected.
public record PlaceOrder(Guid OrderId, Guid PlayerId, string Symbol, OrderSide Side, int Quantity);

public record OrderExecuted(Guid OrderId, Guid PlayerId, string Symbol, OrderSide Side, int Quantity, decimal Price, decimal Total);

public record OrderRejected(Guid OrderId, Guid PlayerId, string Symbol, OrderSide Side, int Quantity, string Reason);

public record Holding(string Symbol, int Quantity);

// Full snapshot of one player's portfolio, published after every change.
public record PortfolioUpdated(Guid PlayerId, string Nickname, int RoundNumber, decimal Cash, Holding[] Holdings);
