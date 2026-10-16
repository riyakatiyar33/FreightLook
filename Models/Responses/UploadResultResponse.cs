namespace Models.Responses;

public class UploadResultResponse
{
    public string FileName { get; set; } = string.Empty;
    public int Parsed { get; set; }
    public int Saved { get; set; }
    public int Rejected { get; set; }

    /// <summary>Human-readable reasons for rejected rows or file-level problems.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>Non-fatal notes, e.g. rows skipped as "call carrier" or surcharges in another currency.</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>Routes that were saved, e.g. "EK BOM-FRA".</summary>
    public List<string> SavedRoutes { get; set; } = [];
}
