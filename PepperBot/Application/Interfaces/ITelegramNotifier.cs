using PepperBot.Domain;

namespace PepperBot.Application.Interfaces;

public interface ITelegramNotifier
{
    Task NotifyNewDealAsync(Deal deal, CancellationToken cancellationToken);
}

