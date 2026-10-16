using Models.Responses;

namespace Service.Interface;

public interface IUploadService
{
    /// <summary>Validates the file, extracts rates, validates each row and saves the valid ones.</summary>
    Task<UploadResultResponse> UploadAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
}
