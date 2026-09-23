using Microsoft.AspNetCore.SignalR;

namespace TradeArena.Gateway;

// Browsers connect to this hub over WebSocket at /hubs/market.
// It's empty for now: the server only pushes to clients. Later, players will call
// methods on it (e.g. BuyStock) that get forwarded to the other services.
public class MarketHub : Hub
{
}
