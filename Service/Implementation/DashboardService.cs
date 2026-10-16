using Data.Repositories.Interfaces;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

public sealed class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default) =>
        _dashboardRepository.GetStatsAsync(cancellationToken);
}
