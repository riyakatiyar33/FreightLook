using System.Data;
using Dapper;
using Data;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

public sealed class DashboardService : IDashboardService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public DashboardService(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<DashboardStatsDto>(new CommandDefinition(
            "dbo.usp_Dashboard_GetStats",
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }
}
