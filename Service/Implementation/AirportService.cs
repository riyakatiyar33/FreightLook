using Data.Repositories.Interfaces;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

public sealed class AirportService : IAirportService
{
    private readonly IAirportRepository _airportRepository;

    public AirportService(IAirportRepository airportRepository)
    {
        _airportRepository = airportRepository;
    }

    public async Task<IReadOnlyList<AirportDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var airports = await _airportRepository.GetAllAsync(cancellationToken);
        return airports
            .Select(a => new AirportDto { Code = a.Code, Name = a.Name, City = a.City, Country = a.Country })
            .ToList();
    }
}
