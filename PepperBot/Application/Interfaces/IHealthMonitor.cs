namespace PepperBot.Application.Interfaces;

public interface IHealthMonitor
{
    /// <summary>
    /// Отметить успешный запрос к Pepper, вернувший более одной скидки.
    /// </summary>
    /// <param name="timestamp">Время успешного запроса (UTC).</param>
    void ReportSuccess(DateTimeOffset timestamp);

    /// <summary>
    /// Время последнего успешного запроса или null, если их ещё не было.
    /// </summary>
    DateTimeOffset? LastSuccessfulRequestAt { get; }

    /// <summary>
    /// Текущее состояние healthcheck.
    /// </summary>
    bool IsHealthy(TimeSpan successTtl);
}

