using PepperBot.Domain;

namespace PepperBot.Application.Interfaces;

public interface IDealRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken);
    Task AddAsync(Deal deal, CancellationToken cancellationToken);
}

