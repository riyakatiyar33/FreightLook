using System.ComponentModel.DataAnnotations;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

/// <summary>
/// Rule-based extraction (no AI): picks the reader for the file type, then parses the
/// resulting grid by header names.
/// </summary>
public sealed class ExtractionService : IExtractionService
{
    private readonly IEnumerable<IRateSheetReader> _readers;

    public ExtractionService(IEnumerable<IRateSheetReader> readers)
    {
        _readers = readers;
    }

    public Task<ExtractionResultDto> ExtractAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var reader = _readers.FirstOrDefault(r => r.SupportedExtensions.Contains(extension))
            ?? throw new ValidationException($"Files of type '{extension}' are not supported.");

        cancellationToken.ThrowIfCancellationRequested();
        var sheet = reader.Read(content, fileName);
        return Task.FromResult(RateSheetParser.Parse(sheet));
    }
}
