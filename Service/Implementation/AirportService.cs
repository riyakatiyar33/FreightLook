using System.Data;
using Dapper;
using Data;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

public sealed class AirportService : IAirportService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AirportService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AirportDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var airports = await connection.QueryAsync<AirportDto>(new CommandDefinition(
            "dbo.usp_Airport_GetAll",
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return airports.ToList();
    }
}
