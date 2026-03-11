using NLog;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Application.Services;

public class DealService
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    private readonly IPepperClient _pepperClient;
    private readonly IDealRepository _repository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ITelegramNotifier _notifier;
    private readonly IHealthMonitor _healthMonitor;

    public DealService(
        IPepperClient pepperClient,
        IDealRepository repository,
        ISubscriptionRepository subscriptionRepository,
        ITelegramNotifier notifier,
        IHealthMonitor healthMonitor)
    {
        _pepperClient = pepperClient;
        _repository = repository;
        _subscriptionRepository = subscriptionRepository;
        _notifier = notifier;
        _healthMonitor = healthMonitor;
    }

    public async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        var deals = await _pepperClient.GetLatestDealsAsync(cancellationToken);

        Logger.Info("Начата обработка полученных скидок: всего {DealCount}", deals.Count);

        // Хелсчек: успешный запрос, вернувший более одной скидки (пусть и не новой)
        if (deals.Count > 1)
        {
            _healthMonitor.ReportSuccess(DateTimeOffset.UtcNow);
            Logger.Debug("HealthCheck: успешно получено более одной скидки ({DealCount})", deals.Count);
        }

        // Загружаем активные подписки один раз на проход
        var subscriptions = await _subscriptionRepository.GetAllActiveAsync(cancellationToken);
        Logger.Info("Загружено активных подписок: {SubscriptionCount}", subscriptions.Count);

        foreach (var deal in deals)
        {
            if (await _repository.ExistsAsync(deal.Id, cancellationToken))
            {
                Logger.Debug("Скидка уже есть в репозитории и будет пропущена: {DealId}", deal.Id);
                continue;
            }

            Logger.Info("Новая скидка: {Title} ({Id})", deal.Title, deal.Id);

            await _repository.AddAsync(deal, cancellationToken);

            // Общая рассылка в основной чат (как было раньше)
            await _notifier.NotifyNewDealAsync(deal, cancellationToken);

            if (subscriptions.Count == 0)
            {
                Logger.Debug("Активные подписки отсутствуют. Фильтрация по ключевым словам пропущена для скидки {DealId}", deal.Id);
                continue;
            }

            var searchableText = $"{deal.Title} {deal.StoreName}";

            foreach (var subscription in subscriptions)
            {
                if (!subscription.IsActive)
                {
                    Logger.Debug("Подписка {SubscriptionId} неактивна и будет пропущена", subscription.Id);
                    continue;
                }

                var keywords = ParseKeywords(subscription.Keywords);
                if (keywords.Count == 0)
                {
                    Logger.Debug("У подписки {SubscriptionId} не удалось извлечь ключевые слова из строки: \"{RawKeywords}\"",
                        subscription.Id,
                        subscription.Keywords);
                    continue;
                }

                var isMatch = IsMatch(searchableText, keywords, out var matchedKeyword);
                Logger.Debug(
                    "Результат фильтрации скидки {DealId} по подписке {SubscriptionId}: {IsMatch}. Текст: \"{Text}\"; ключевые слова: {Keywords}",
                    deal.Id,
                    subscription.Id,
                    isMatch,
                    searchableText,
                    string.Join(", ", keywords));

                if (isMatch)
                {
                    var selectionRule =
                        $"совпадение по ключевому слову \"{matchedKeyword}\" (подписка [{subscription.Id}]: {subscription.Keywords})";
                    await _notifier.NotifyDealToChatAsync(deal, subscription.ChatId, selectionRule, cancellationToken);
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

    private static bool IsMatch(string text, List<string> keywords, out string matchedKeyword)
    {
        matchedKeyword = string.Empty;
        if (keywords.Count == 0)
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchedKeyword = keyword;
                return true;
            }
        }

        return false;
    }
}

