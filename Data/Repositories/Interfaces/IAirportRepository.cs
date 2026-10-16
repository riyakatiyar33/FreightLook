using Models.Entities;

namespace Data.Repositories.Interfaces;

public interface IAirportRepository
{
    Task<IReadOnlyList<Airport>> GetAllAsync(CancellationToken cancellationToken = default);
}
