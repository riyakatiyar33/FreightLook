using Models.Dtos;

namespace Service.Interface;

public interface IExtractionService
{
    /// <summary>
    /// Extracts rate rows from a rate sheet (PDF or image). Output is unvalidated;
    /// the caller is responsible for validation.
    /// </summary>
    Task<ExtractionResultDto> ExtractAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default);
}
