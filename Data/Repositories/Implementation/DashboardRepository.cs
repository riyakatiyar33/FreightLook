using System.Data;
using Dapper;
using Data.Repositories.Interfaces;
using Models.Dtos;

namespace Data.Repositories.Implementation;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DashboardRepository(IDbConnectionFactory connectionFactory)
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
