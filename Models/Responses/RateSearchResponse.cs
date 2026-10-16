using Models.Dtos;

namespace Models.Responses;

public class RateSearchResponse
{
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public int Count => Results.Count;
    public List<RateSearchResultDto> Results { get; set; } = [];
}
