using Models.Dtos;

namespace Data.Repositories.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
