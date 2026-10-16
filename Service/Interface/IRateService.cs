using Models.Requests;
using Models.Responses;

namespace Service.Interface;

public interface IRateService
{
    /// <summary>Searches active rates for a route and, when a weight is given, prices each one.</summary>
    Task<RateSearchResponse> SearchAsync(RateSearchRequest request, CancellationToken cancellationToken = default);
}
