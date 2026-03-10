using PepperBot.Domain;

namespace PepperBot.Application.Interfaces;

public interface IPepperClient
{
    Task<IReadOnlyList<Deal>> GetLatestDealsAsync(CancellationToken cancellationToken);
}

