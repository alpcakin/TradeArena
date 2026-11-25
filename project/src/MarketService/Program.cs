using MassTransit;
using TradeArena.MarketService;

var builder = Host.CreateApplicationBuilder(args);

// Connect to RabbitMQ. "localhost" when running with `dotnet run`,
// "rabbitmq" (the container name) when running inside Docker Compose.
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHostedService<MarketSimulator>();

builder.Build().Run();
