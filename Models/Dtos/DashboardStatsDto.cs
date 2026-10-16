namespace Models.Dtos;

public class DashboardStatsDto
{
    public int AirlineCount { get; set; }
    public int RouteCount { get; set; }
    public int RateCount { get; set; }
    public string? LastUploadFileName { get; set; }
    public DateTime? LastUploadAt { get; set; }
    public int? LastUploadSavedCount { get; set; }
}
