using Models.Entities;

namespace Data.Repositories.Interfaces;

public interface IRateSheetUploadRepository
{
    Task<int> InsertAsync(RateSheetUpload upload, CancellationToken cancellationToken = default);
}
