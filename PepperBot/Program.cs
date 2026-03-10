using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PepperBot;
using PepperBot.Application.Interfaces;
using PepperBot.Application.Services;
using PepperBot.Infrastructure.Data;
using PepperBot.Infrastructure.Health;
using PepperBot.Infrastructure.Pepper;
using PepperBot.Infrastructure.Telegram;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddSingleton<IDealRepository, SqliteDealRepository>();
builder.Services.AddSingleton<IPepperClient, PepperClient>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<IHealthMonitor, PepperHealthMonitor>();
builder.Services.AddSingleton<DealService>();

builder.Services.AddHostedService<BotWorker>();

var app = builder.Build();

app.MapGet("/health", (IHealthMonitor healthMonitor) =>
{
    var successTtl = TimeSpan.FromMinutes(15);
    var isHealthy = healthMonitor.IsHealthy(successTtl);

    if (isHealthy)
    {
        return Results.Ok(new
        {
            status = "Healthy",
            lastSuccessfulRequestAt = healthMonitor.LastSuccessfulRequestAt
        });
    }

    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

await app.RunAsync();
