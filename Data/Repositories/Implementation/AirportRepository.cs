using System.Data;
using Dapper;
using Data.Repositories.Interfaces;
using Models.Entities;

namespace Data.Repositories.Implementation;

public sealed class AirportRepository : IAirportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AirportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Airport>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var airports = await connection.QueryAsync<Airport>(new CommandDefinition(
            "dbo.usp_Airport_GetAll",
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return airports.ToList();
    }
}
