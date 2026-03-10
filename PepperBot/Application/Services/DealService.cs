using Microsoft.Extensions.Logging;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Application.Services;

public class DealService
{
    private readonly IPepperClient _pepperClient;
    private readonly IDealRepository _repository;
    private readonly ITelegramNotifier _notifier;
    private readonly ILogger<DealService> _logger;
    private readonly IHealthMonitor _healthMonitor;

    public DealService(
        IPepperClient pepperClient,
        IDealRepository repository,
        ITelegramNotifier notifier,
        ILogger<DealService> logger,
        IHealthMonitor healthMonitor)
    {
        _pepperClient = pepperClient;
        _repository = repository;
        _notifier = notifier;
        _logger = logger;
        _healthMonitor = healthMonitor;
    }

    public async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        var deals = await _pepperClient.GetLatestDealsAsync(cancellationToken);

        // Хелсчек: успешный запрос, вернувший более одной скидки (пусть и не новой)
        if (deals.Count > 1)
        {
            _healthMonitor.ReportSuccess(DateTimeOffset.UtcNow);
        }

        foreach (var deal in deals)
        {
            if (await _repository.ExistsAsync(deal.Id, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation("Новая скидка: {Title} ({Id})", deal.Title, deal.Id);

            await _repository.AddAsync(deal, cancellationToken);
            await _notifier.NotifyNewDealAsync(deal, cancellationToken);
        }
    }
}

