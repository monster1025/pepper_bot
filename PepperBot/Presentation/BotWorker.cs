using Microsoft.Extensions.Hosting;
using NLog;
using PepperBot.Application.Interfaces;
using PepperBot.Application.Services;

namespace PepperBot;

public class BotWorker : BackgroundService
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    private readonly DealService _dealService;
    private readonly IDealRepository _repository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITelegramNotifier _telegramNotifier;

    public BotWorker(
        DealService dealService,
        IDealRepository repository,
        ISubscriptionRepository subscriptionRepository,
        ITelegramNotifier telegramNotifier)
    {
        _dealService = dealService;
        _repository = repository;
        _subscriptionRepository = subscriptionRepository;
        _telegramNotifier = telegramNotifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _repository.InitializeAsync(stoppingToken);
        await _subscriptionRepository.InitializeAsync(stoppingToken);

        // Вся работа с Telegram вынесена в TelegramNotifier
        await _telegramNotifier.StartAsync(stoppingToken);

        Logger.Info("PepperBot запущен. Опрос скидок каждые 30 секунд.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dealService.CheckAndNotifyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при обработке скидок");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
