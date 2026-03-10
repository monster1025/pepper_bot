using PepperBot.Domain;

namespace PepperBot.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Получить все активные подписки.
    /// </summary>
    Task<IReadOnlyList<Subscription>> GetAllActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Добавить новое правило подписки для указанного чата.
    /// Одно правило = один кейворд (но у одного чата может быть много правил).
    /// </summary>
    Task AddSubscriptionAsync(string chatId, string keyword, CancellationToken cancellationToken);

    /// <summary>
    /// Получить активные подписки для конкретного чата.
    /// </summary>
    Task<IReadOnlyList<Subscription>> GetByChatAsync(string chatId, CancellationToken cancellationToken);

    /// <summary>
    /// Удалить (деактивировать) подписку по идентификатору.
    /// </summary>
    Task DeleteSubscriptionAsync(long id, CancellationToken cancellationToken);
}

