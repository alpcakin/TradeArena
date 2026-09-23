using MassTransit;
using TradeArena.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// The Gateway subscribes to events from the other services and forwards them to browsers.
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MarketTickConsumer>();

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

var app = builder.Build();

app.UseDefaultFiles();  // serves wwwroot/index.html at "/"
app.UseStaticFiles();
app.MapHub<MarketHub>("/hubs/market");

app.Run();
