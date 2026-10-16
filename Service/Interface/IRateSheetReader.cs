using Models.Dtos;

namespace Service.Interface;

/// <summary>Reads one file format (PDF, Excel, ...) into a format-independent grid of text cells.</summary>
public interface IRateSheetReader
{
    /// <summary>Lower-case file extensions handled by this reader, e.g. ".pdf".</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    RawSheetDto Read(byte[] content, string fileName);
}
