using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Models.Responses;
using Service.Interface;

namespace Backend.Controllers;

[ApiController]
[Route("api/rates")]
[Produces("application/json")]
public class RatesController : ControllerBase
{
    private readonly IRateService _rateService;

    public RatesController(IRateService rateService)
    {
        _rateService = rateService;
    }

    /// <summary>Searches active rates for a route. With a weight, each rate is priced using its applicable slab.</summary>
    /// <remarks>Example: GET /api/rates/search?origin=BOM&amp;destination=FRA&amp;weight=320</remarks>
    [HttpGet("search")]
    [ProducesResponseType<RateSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RateSearchResponse>> Search([FromQuery] RateSearchRequest request, CancellationToken cancellationToken)
    {
        var response = await _rateService.SearchAsync(request, cancellationToken);
        return Ok(response);
    }
}
