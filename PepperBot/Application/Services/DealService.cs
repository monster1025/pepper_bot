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

    public DealService(
        IPepperClient pepperClient,
        IDealRepository repository,
        ITelegramNotifier notifier,
        ILogger<DealService> logger)
    {
        _pepperClient = pepperClient;
        _repository = repository;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        var deals = await _pepperClient.GetLatestDealsAsync(cancellationToken);

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

