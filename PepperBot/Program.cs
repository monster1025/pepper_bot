using PepperBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PepperBot.Application.Services;
using PepperBot.Application.Interfaces;
using PepperBot.Infrastructure.Data;
using PepperBot.Infrastructure.Pepper;
using PepperBot.Infrastructure.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddSingleton<IDealRepository, SqliteDealRepository>();
builder.Services.AddSingleton<IPepperClient, PepperClient>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<DealService>();

builder.Services.AddHostedService<BotWorker>();

var host = builder.Build();

await host.RunAsync();
