using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TradeArena.Contracts;

namespace TradeArena.Gateway;

// Browsers connect to this hub over WebSocket at /hubs/arena.
// The hub turns player actions into messages; it never changes game data itself.
public class ArenaHub(IPublishEndpoint publisher, ISendEndpointProvider sender) : Hub
{
    private const string PlayerIdKey = "playerId";

    public static string PlayerGroup(Guid playerId) => $"player:{playerId}";

    // One-click sign in: a nickname is enough. A returning browser sends the id it got the first
    // time, so the player keeps the same portfolio after a page reload.
    public async Task<Guid> Join(string nickname, Guid? playerId)
    {
        nickname = (nickname ?? "").Trim();
        if (nickname.Length is < 2 or > 16 || nickname.Any(char.IsControl))
            throw new HubException("The nickname must be 2 to 16 characters long.");

        var id = playerId is { } existing && existing != Guid.Empty ? existing : Guid.NewGuid();

        if (Context.Items[PlayerIdKey] is Guid previous && previous != id)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PlayerGroup(previous));

        // Messages for one player go to this group, which covers every tab the player has open.
        Context.Items[PlayerIdKey] = id;
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(id));

        await publisher.Publish(new PlayerJoined(id, nickname));
        return id;
    }

    public async Task<Guid> PlaceOrder(string symbol, OrderSide side, int quantity)
    {
        if (Context.Items[PlayerIdKey] is not Guid playerId)
            throw new HubException("Join the arena first.");
        if (quantity is < 1 or > 10_000)
            throw new HubException("The quantity must be between 1 and 10,000.");

        // The order is a command for the Portfolio Service. The result comes back later
        // as an OrderExecuted or OrderRejected event.
        var orderId = Guid.NewGuid();
        await sender.Send(new PlaceOrder(orderId, playerId, symbol.Trim().ToUpperInvariant(), side, quantity));
        return orderId;
    }
}
