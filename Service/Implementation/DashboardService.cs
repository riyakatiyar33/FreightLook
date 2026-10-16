using System.Data;
using Dapper;
using Data;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

public sealed class DashboardService : IDashboardService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DashboardService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<DashboardStatsDto>(new CommandDefinition(
            "dbo.usp_Dashboard_GetStats",
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }
}
