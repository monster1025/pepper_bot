namespace PepperBot.Domain;

public class Subscription
{
    public long Id { get; set; }

    /// <summary>
    /// Telegram chat id пользователя (может быть user id или id группы).
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// Строка с ключевыми словами, разделёнными запятыми/пробелами (например: "PLA, ABS, PETG").
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

