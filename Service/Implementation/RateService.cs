using System.ComponentModel.DataAnnotations;
using System.Data;
using Dapper;
using Data;
using Models.Constants;
using Models.Dtos;
using Models.Entities;
using Models.Requests;
using Models.Responses;
using Service.Interface;

namespace Service.Implementation;

public sealed class RateService : IRateService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISlabResolver _slabResolver;

    public RateService(IDbConnectionFactory connectionFactory, ISlabResolver slabResolver)
    {
        _connectionFactory = connectionFactory;
        _slabResolver = slabResolver;
    }

    public async Task<RateSearchResponse> SearchAsync(RateSearchRequest request, CancellationToken cancellationToken = default)
    {
        var origin = request.Origin.Trim().ToUpperInvariant();
        var destination = request.Destination.Trim().ToUpperInvariant();

        if (origin == destination)
            throw new ValidationException("Origin and destination must be different.");

        if (request.Weight is <= 0)
            throw new ValidationException("Weight must be greater than zero.");

        // Always hits the database (no caching) so freshly uploaded rates show up immediately.
        var rates = await LoadRatesAsync(origin, destination, cancellationToken);

        var results = rates.Select(rate => ToResult(rate, request.Weight)).ToList();

        results = request.Weight.HasValue
            ? results.OrderBy(r => r.EstimatedTotal ?? decimal.MaxValue).ThenBy(r => r.AirlineName).ToList()
            : results.OrderBy(r => r.AirlineName).ThenBy(r => r.Commodity).ToList();

        return new RateSearchResponse
        {
            Origin = origin,
            Destination = destination,
            Weight = request.Weight,
            Results = results
        };
    }

    /// <summary>
    /// usp_Rate_Search returns three result sets (rates, slabs, surcharges);
    /// slabs and surcharges are attached to their rate by RateId.
    /// </summary>
    private async Task<List<Rate>> LoadRatesAsync(string origin, string destination, CancellationToken cancellationToken)
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

    private RateSearchResultDto ToResult(Rate rate, decimal? weight)
    {
        var result = new RateSearchResultDto
        {
            RateId = rate.RateId,
            AirlineCode = rate.AirlineCode,
            AirlineName = rate.AirlineName,
            OriginCode = rate.OriginCode,
            OriginName = rate.OriginName,
            DestinationCode = rate.DestinationCode,
            DestinationName = rate.DestinationName,
            Commodity = rate.Commodity,
            Currency = rate.Currency,
            MinimumCharge = rate.MinimumCharge,
            UpdatedAt = rate.CreatedAt,
            Slabs = rate.Slabs.ToDictionary(s => s.SlabCode, s => s.PricePerKg),
            Surcharges = rate.Surcharges
                .OrderBy(s => IndexOf(SurchargeTypes.All, s.SurchargeType))
                .Select(s => new SurchargeDto { Type = s.SurchargeType, Amount = s.Amount, MinAmount = s.MinAmount, Basis = s.Basis })
                .ToList()
        };

        if (weight is not { } w)
            return result;

        var slab = _slabResolver.Resolve(w, result.Slabs.Keys);
        if (slab is null)
            return result;

        var freight = Math.Max(rate.MinimumCharge, result.Slabs[slab] * w);
        // Per-kg surcharges respect their own minimum (e.g. X-ray 4.00/kg, min 300); flat ones are added once.
        var surcharges = rate.Surcharges.Sum(s =>
            s.Basis == SurchargeBasis.PerShipment ? s.Amount : Math.Max(s.Amount * w, s.MinAmount ?? 0m));

        result.ApplicableSlab = slab;
        result.FreightCharge = Math.Round(freight, 2, MidpointRounding.AwayFromZero);
        result.SurchargeTotal = Math.Round(surcharges, 2, MidpointRounding.AwayFromZero);
        result.EstimatedTotal = result.FreightCharge + result.SurchargeTotal;
        return result;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return int.MaxValue;
    }
}
