using Models.Entities;

namespace Data.Repositories.Interfaces;

public interface IRateRepository
{
    /// <summary>Saves a rate with its slabs and surcharges (usp_Rate_Insert). Returns the new RateId.</summary>
    Task<int> InsertAsync(Rate rate, CancellationToken cancellationToken = default);

    /// <summary>Active rates for a route with slabs and surcharges populated (usp_Rate_Search).</summary>
    Task<IReadOnlyList<Rate>> SearchAsync(string? origin, string? destination, CancellationToken cancellationToken = default);
}
