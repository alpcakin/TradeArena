using System.Text.Json.Serialization;
using MassTransit;
using TradeArena.Contracts;
using TradeArena.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Read-only queries (current round, past rounds) are forwarded to the Game Service over HTTP.
builder.Services.AddHttpClient("game", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Game"] ?? "http://localhost:5103");
    client.Timeout = TimeSpan.FromSeconds(3);
});

// A command has exactly one receiver, so orders are sent straight to the Portfolio Service's queue.
EndpointConvention.Map<PlaceOrder>(new Uri("queue:portfolio"));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BroadcastConsumer>();
    x.AddConsumer<PlayerEventsConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // Each Gateway instance gets its own temporary queue, so every instance receives every
        // event and pushes it to the browsers connected to it.
        cfg.ReceiveEndpoint(new TemporaryEndpointDefinition("gateway"), e => e.ConfigureConsumers(context));
    });
});

var app = builder.Build();

app.UseDefaultFiles();  // serves wwwroot/index.html at "/"
app.UseStaticFiles();
app.MapHub<ArenaHub>("/hubs/arena");

app.MapGet("/api/rounds/current", (IHttpClientFactory http, CancellationToken ct) => ForwardToGame(http, "rounds/current", ct));
app.MapGet("/api/rounds", (IHttpClientFactory http, CancellationToken ct) => ForwardToGame(http, "rounds", ct));

app.Run();

static async Task<IResult> ForwardToGame(IHttpClientFactory http, string path, CancellationToken ct)
{
    try
    {
        using var response = await http.CreateClient("game").GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
    {
        return Results.Problem("The game service is not reachable right now.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
