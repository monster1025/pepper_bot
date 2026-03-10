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
using Telegram.Bot;
using NLog.Web;

var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

builder.Services.AddSingleton<IDealRepository, SqliteDealRepository>();
builder.Services.AddSingleton<ISubscriptionRepository, SqliteSubscriptionRepository>();
builder.Services.AddSingleton<IPepperClient, PepperClient>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<IHealthMonitor, PepperHealthMonitor>();
builder.Services.AddSingleton<DealService>();

// Клиент Telegram-бота для приёма личных сообщений
builder.Services.AddSingleton<ITelegramBotClient>(_ =>
{
    var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? string.Empty;
    return new TelegramBotClient(token);
});

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

    logger.LogInformation("Запуск PepperBot приложения");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Приложение остановлено из-за необработанного исключения");
    throw;
}
finally
{
    global::NLog.LogManager.Shutdown();
}
