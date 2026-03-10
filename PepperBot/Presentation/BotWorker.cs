using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PepperBot.Application.Interfaces;
using PepperBot.Application.Services;

namespace PepperBot;

public class BotWorker : BackgroundService
{
    private readonly DealService _dealService;
    private readonly IDealRepository _repository;
    private readonly ILogger<BotWorker> _logger;

    public BotWorker(
        DealService dealService,
        IDealRepository repository,
        ILogger<BotWorker> logger)
    {
        _dealService = dealService;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _repository.InitializeAsync(stoppingToken);

        _logger.LogInformation("PepperBot запущен. Опрос каждые 30 секунд.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dealService.CheckAndNotifyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке скидок");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

