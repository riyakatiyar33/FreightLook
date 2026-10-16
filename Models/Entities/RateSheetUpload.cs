namespace Models.Entities;

public class RateSheetUpload
{
    public int RateSheetUploadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ParsedCount { get; set; }
    public int SavedCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTime UploadedAt { get; set; }
}
