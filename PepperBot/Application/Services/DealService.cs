using Microsoft.Extensions.Logging;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Application.Services;

public class DealService
{
    private readonly IPepperClient _pepperClient;
    private readonly IDealRepository _repository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITelegramNotifier _notifier;
    private readonly ILogger<DealService> _logger;
    private readonly IHealthMonitor _healthMonitor;

    public DealService(
        IPepperClient pepperClient,
        IDealRepository repository,
        ISubscriptionRepository subscriptionRepository,
        ITelegramNotifier notifier,
        ILogger<DealService> logger,
        IHealthMonitor healthMonitor)
    {
        _pepperClient = pepperClient;
        _repository = repository;
        _subscriptionRepository = subscriptionRepository;
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

        // Загружаем активные подписки один раз на проход
        var subscriptions = await _subscriptionRepository.GetAllActiveAsync(cancellationToken);

        foreach (var deal in deals)
        {
            if (await _repository.ExistsAsync(deal.Id, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation("Новая скидка: {Title} ({Id})", deal.Title, deal.Id);

            await _repository.AddAsync(deal, cancellationToken);

            // Общая рассылка в основной чат (как было раньше)
            await _notifier.NotifyNewDealAsync(deal, cancellationToken);

            if (subscriptions.Count == 0)
            {
                continue;
            }

            var searchableText = $"{deal.Title} {deal.StoreName}";

            foreach (var subscription in subscriptions)
            {
                if (!subscription.IsActive)
                {
                    continue;
                }

                var keywords = ParseKeywords(subscription.Keywords);
                if (keywords.Count == 0)
                {
                    continue;
                }

                if (IsMatch(searchableText, keywords))
                {
                    await _notifier.NotifyDealToChatAsync(deal, subscription.ChatId, cancellationToken);
                }
            }
        }
    }

    private static List<string> ParseKeywords(string raw)
    {
        return raw
            .Split(new[] { '-',',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .ToList();
    }

    private static bool IsMatch(string text, List<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

