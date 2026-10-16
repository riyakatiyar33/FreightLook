using System.Data;
using Dapper;
using Data.Repositories.Interfaces;
using Models.Entities;

namespace Data.Repositories.Implementation;

public sealed class RateRepository : IRateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RateRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InsertAsync(Rate rate, CancellationToken cancellationToken = default)
    {
        var slabs = new DataTable();
        slabs.Columns.Add("SlabCode", typeof(string));
        slabs.Columns.Add("PricePerKg", typeof(decimal));
        foreach (var slab in rate.Slabs)
            slabs.Rows.Add(slab.SlabCode, slab.PricePerKg);

        var surcharges = new DataTable();
        surcharges.Columns.Add("SurchargeType", typeof(string));
        surcharges.Columns.Add("Amount", typeof(decimal));
        surcharges.Columns.Add("MinAmount", typeof(decimal));
        surcharges.Columns.Add("Basis", typeof(string));
        foreach (var surcharge in rate.Surcharges)
            surcharges.Rows.Add(surcharge.SurchargeType, surcharge.Amount, (object?)surcharge.MinAmount ?? DBNull.Value, surcharge.Basis);

        var parameters = new DynamicParameters();
        parameters.Add("@AirlineCode", rate.AirlineCode);
        parameters.Add("@AirlineName", string.IsNullOrWhiteSpace(rate.AirlineName) ? null : rate.AirlineName);
        parameters.Add("@OriginCode", rate.OriginCode);
        parameters.Add("@DestinationCode", rate.DestinationCode);
        parameters.Add("@Commodity", rate.Commodity ?? string.Empty);
        parameters.Add("@Currency", rate.Currency);
        parameters.Add("@MinimumCharge", rate.MinimumCharge);
        parameters.Add("@SourceFile", rate.SourceFile);
        parameters.Add("@Slabs", slabs.AsTableValuedParameter("dbo.RateSlabTableType"));
        parameters.Add("@Surcharges", surcharges.AsTableValuedParameter("dbo.SurchargeTableType"));
        parameters.Add("@RateId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Rate_Insert", parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<int>("@RateId");
    }

    public async Task<IReadOnlyList<Rate>> SearchAsync(string? origin, string? destination, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_Rate_Search",
            new { Origin = origin, Destination = destination },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var rates = (await grid.ReadAsync<Rate>()).ToList();
        var slabs = (await grid.ReadAsync<RateSlab>()).ToLookup(s => s.RateId);
        var surcharges = (await grid.ReadAsync<Surcharge>()).ToLookup(s => s.RateId);

        foreach (var rate in rates)
        {
            rate.Slabs = slabs[rate.RateId].ToList();
            rate.Surcharges = surcharges[rate.RateId].ToList();
        }

        return rates;
    }
}
