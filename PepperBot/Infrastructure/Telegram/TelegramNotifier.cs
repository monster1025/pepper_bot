using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Infrastructure.Telegram;

public class TelegramNotifier : ITelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotifier> _logger;
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramNotifier(ILogger<TelegramNotifier> logger)
    {
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? string.Empty;
        _chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId))
        {
            _logger.LogWarning("TELEGRAM_BOT_TOKEN или TELEGRAM_CHAT_ID не заданы. Отправка сообщений отключена.");
        }

        _httpClient = new HttpClient();
    }

    public async Task NotifyNewDealAsync(Deal deal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId))
        {
            return;
        }

        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        var textBuilder = new StringBuilder();
        textBuilder.AppendLine($"🔥 *{Escape(deal.Title)}*");

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
        textBuilder.AppendLine($"Магазин: {Escape(deal.StoreName)}");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"[Открыть на Pepper.ru]({Escape(deal.DealUrl)})");

        var payload = new
        {
            chat_id = _chatId,
            text = textBuilder.ToString(),
            parse_mode = "Markdown"
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Не удалось отправить сообщение в Telegram: {Status} {Body}", response.StatusCode, body);
        }
    }

    private static string Escape(string value)
    {
        return value
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");
    }
}

