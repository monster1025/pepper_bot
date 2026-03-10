using System.Globalization;
using System.Text;
using NLog;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PepperBot.Infrastructure.Telegram;

public class TelegramNotifier : ITelegramNotifier
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    private readonly ITelegramBotClient _botClient;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly string _broadcastChatId;
    private bool _started;

    private enum ConversationState
    {
        None = 0,
        AwaitingAddKeyword = 1
    }

    private readonly Dictionary<string, ConversationState> _chatStates = new();

    public TelegramNotifier(
        ITelegramBotClient botClient,
        ISubscriptionRepository subscriptionRepository)
    {
        _botClient = botClient;
        _subscriptionRepository = subscriptionRepository;
        _broadcastChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_broadcastChatId))
        {
            Logger.Info("TELEGRAM_CHAT_ID не задан. Общая рассылка по скидкам будет отключена.");
        }
    }

    public Task NotifyNewDealAsync(Deal deal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_broadcastChatId))
        {
            return Task.CompletedTask;
        }

        return SendDealAsync(deal, _broadcastChatId, cancellationToken);
    }

    public Task NotifyDealToChatAsync(Deal deal, string chatId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return Task.CompletedTask;
        }

        return SendDealAsync(deal, chatId, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return Task.CompletedTask;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken);

        _started = true;
        Logger.Info("Запущена обработка входящих сообщений Telegram.");

        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.CallbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, update.CallbackQuery!, cancellationToken);
        {
            return;
        }
        }

        if (update.Type != UpdateType.Message)
        {
            return;
        }

        var message = update.Message;
        if (message is null)
        {
            return;
        }

        if (message.Type != MessageType.Text)
        {
            return;
        }

        // Нас интересуют только личные сообщения боту
        if (message.Chat.Type != ChatType.Private)
        {
            return;
        }

        var chatId = message.Chat.Id.ToString(CultureInfo.InvariantCulture);
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Команда для вызова меню
        if (text.Equals("/menu", StringComparison.OrdinalIgnoreCase))
        {
            await SendMainMenuAsync(message.Chat.Id, cancellationToken);
            _chatStates[chatId] = ConversationState.None;
            return;
        }

        // Обработка выбора пунктов меню
        if (text.Equals("Добавить подписку", StringComparison.OrdinalIgnoreCase))
        {
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Отправьте одно или несколько ключевых слов для подписки (через пробел, запятую или с новой строки).",
                cancellationToken: cancellationToken);

            _chatStates[chatId] = ConversationState.AwaitingAddKeyword;
            return;
        }

        if (text.Equals("Удалить подписку", StringComparison.OrdinalIgnoreCase))
        {
            await ShowDeleteMenuAsync(message.Chat.Id, chatId, cancellationToken);
            _chatStates[chatId] = ConversationState.None;
            return;
        }

        if (text.Equals("Список подписок", StringComparison.OrdinalIgnoreCase))
        {
            await ShowSubscriptionsListAsync(message.Chat.Id, chatId, cancellationToken);
            _chatStates[chatId] = ConversationState.None;
            return;
        }

        // Обработка состояний диалога
        _chatStates.TryGetValue(chatId, out var state);

        if (state == ConversationState.AwaitingAddKeyword)
        {
            var separators = new[] { ',', ';', '\n', '\r', '\t', ' ' };
            var keywords = text
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();

            if (keywords.Count == 0)
            {
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Не нашёл ни одного ключевого слова. Попробуйте ещё раз или нажмите /menu.",
                    cancellationToken: cancellationToken);
                return;
            }

            foreach (var keyword in keywords)
            {
                await _subscriptionRepository.AddSubscriptionAsync(chatId, keyword, cancellationToken);
            }

            _chatStates[chatId] = ConversationState.None;

            var confirmation = new StringBuilder();
            confirmation.AppendLine("Добавлены правила подписки по ключевым словам:");
            confirmation.AppendLine(string.Join(", ", keywords));

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: confirmation.ToString(),
                cancellationToken: cancellationToken);

            Logger.Info(
                "Для чата {ChatId} добавлены правила подписки по ключевым словам: {Keywords}",
                chatId,
                string.Join(", ", keywords));

            return;
        }

        // Если состояние не распознано — просто напоминаем про меню
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Используйте команду /menu для управления подписками.",
            cancellationToken: cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            return;
        }

        if (callbackQuery.Data.StartsWith("del:", StringComparison.Ordinal))
        {
            var idPart = callbackQuery.Data["del:".Length..];
            if (!long.TryParse(idPart, CultureInfo.InvariantCulture, out var id))
            {
                return;
            }

            await _subscriptionRepository.DeleteSubscriptionAsync(id, cancellationToken);

            await botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Подписка удалена.",
                cancellationToken: cancellationToken);

            if (callbackQuery.Message is not null)
            {
                var chatId = callbackQuery.Message.Chat.Id;
                var chatIdString = chatId.ToString(CultureInfo.InvariantCulture);
                await ShowDeleteMenuAsync(chatId, chatIdString, cancellationToken);
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Ошибка Telegram API: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };

        Logger.Error("Ошибка Telegram-бота: {Error}", errorMessage);
        return Task.CompletedTask;
    }

    private async Task SendDealAsync(Deal deal, string chatId, CancellationToken cancellationToken)
    {
        var textBuilder = new StringBuilder();
        textBuilder.AppendLine($"🔥 {deal.Title}");

        if (deal.CurrentPrice is not null)
        {
            textBuilder.AppendLine($"Цена: {deal.CurrentPrice:0.##} ₽");
        }

        if (deal.RetailPrice is not null && deal.RetailPrice > 0 && deal.CurrentPrice is not null)
        {
            textBuilder.AppendLine($"Старая цена: {deal.RetailPrice:0.##} ₽");
        }

        if (deal.PercentOff is not null)
        {
            textBuilder.AppendLine($"Скидка: {deal.PercentOff}%");
        }

        textBuilder.AppendLine();
        textBuilder.AppendLine($"Магазин: {deal.StoreName}");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"Товар в магазине: {deal.DealUrl}");

        if (!string.IsNullOrEmpty(deal.Permalink))
        {
            textBuilder.AppendLine($"Открыть на Pepper.ru: https://www.pepper.ru/deals/{deal.Permalink}");
        }

        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: textBuilder.ToString(),
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            Logger.Warn(
                ex,
                "Не удалось отправить сообщение в Telegram (чат {ChatId}): [{Code}] {Message}",
                chatId,
                ex.ErrorCode,
                ex.Message);
        }
    }

    private Task SendMainMenuAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("Добавить подписку") },
            new[] { new KeyboardButton("Удалить подписку") },
            new[] { new KeyboardButton("Список подписок") }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        return _botClient.SendMessage(
            chatId: chatId,
            text: "Меню управления подписками:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ShowSubscriptionsListAsync(ChatId chatId, string chatIdString, CancellationToken cancellationToken)
    {
        var subs = await _subscriptionRepository.GetByChatAsync(chatIdString, cancellationToken);

        if (subs.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "У вас пока нет подписок.",
                cancellationToken: cancellationToken);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Ваши подписки:");
        foreach (var sub in subs)
        {
            sb.AppendLine($"• [{sub.Id}] {sub.Keywords}");
        }

        await _botClient.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            cancellationToken: cancellationToken);
    }

    private async Task ShowDeleteMenuAsync(ChatId chatId, string chatIdString, CancellationToken cancellationToken)
    {
        var subs = await _subscriptionRepository.GetByChatAsync(chatIdString, cancellationToken);

        if (subs.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Подписок для удаления не найдено.",
                cancellationToken: cancellationToken);
            return;
        }

        var buttons = subs
            .Select(s => InlineKeyboardButton.WithCallbackData(
                text: s.Keywords,
                callbackData: $"del:{s.Id}"))
            .Chunk(2)
            .Select(chunk => chunk.ToArray())
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons);

        await _botClient.SendMessage(
            chatId: chatId,
            text: "Выберите подписку для удаления:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}

