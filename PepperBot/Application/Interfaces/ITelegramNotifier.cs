using PepperBot.Domain;

namespace PepperBot.Application.Interfaces;

public interface ITelegramNotifier
{
    /// <summary>
    /// Уведомление о новой скидке в общий чат (из переменной окружения TELEGRAM_CHAT_ID).
    /// </summary>
    Task NotifyNewDealAsync(Deal deal, CancellationToken cancellationToken);

    /// <summary>
    /// Уведомление о скидке в конкретный Telegram-чат (личная/персональная рассылка).
    /// </summary>
    Task NotifyDealToChatAsync(Deal deal, string chatId, string? selectionRule, CancellationToken cancellationToken);

    /// <summary>
    /// Запустить обработку входящих сообщений Telegram и регистрацию подписок.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);
}

