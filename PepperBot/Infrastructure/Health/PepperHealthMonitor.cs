using System.Threading;
using PepperBot.Application.Interfaces;

namespace PepperBot.Infrastructure.Health;

public class PepperHealthMonitor : IHealthMonitor
{
    private DateTimeOffset? _lastSuccessfulRequestAt;

    public DateTimeOffset? LastSuccessfulRequestAt => _lastSuccessfulRequestAt;

    public void ReportSuccess(DateTimeOffset timestamp)
    {
        // Просто запоминаем последнее успешное время; избыточную синхронизацию не вводим
        // так как запись идёт по одному воркеру.
        _lastSuccessfulRequestAt = timestamp;
    }

    public bool IsHealthy(TimeSpan successTtl)
    {
        var last = _lastSuccessfulRequestAt;
        if (last is null)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - last.Value < successTtl;
    }
}

