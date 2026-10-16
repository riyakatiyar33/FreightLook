using Models.Dtos;

namespace Service.Interface;

public interface IAirportService
{
    Task<IReadOnlyList<AirportDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
