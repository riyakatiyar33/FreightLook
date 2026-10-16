using Microsoft.AspNetCore.Mvc;
using Models.Dtos;
using Service.Interface;

namespace Backend.Controllers;

[ApiController]
[Route("api/airports")]
[Produces("application/json")]
public class AirportsController : ControllerBase
{
    private readonly IAirportService _airportService;

    public AirportsController(IAirportService airportService)
    {
        _airportService = airportService;
    }

    /// <summary>All airports, ordered by IATA code (used for From/To autocomplete).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AirportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AirportDto>>> GetAll(CancellationToken cancellationToken)
    {
        var airports = await _airportService.GetAllAsync(cancellationToken);
        return Ok(airports);
    }
}
