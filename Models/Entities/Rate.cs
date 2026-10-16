namespace Models.Entities;

/// <summary>
/// A rate for one airline on one route. Carries the airline/airport codes and names
/// so it can be saved by code and returned from search without extra lookups.
/// </summary>
public class Rate
{
    public int RateId { get; set; }

    public int AirlineId { get; set; }
    public string AirlineCode { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;

    public string OriginCode { get; set; } = string.Empty;
    public string OriginName { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;

    /// <summary>Special commodity / product (Courier, DGR, Perishable, ...). Empty = general cargo.</summary>
    public string Commodity { get; set; } = string.Empty;

    public string Currency { get; set; } = "USD";
    public decimal MinimumCharge { get; set; }
    public string? SourceFile { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<RateSlab> Slabs { get; set; } = [];
    public List<Surcharge> Surcharges { get; set; } = [];
}
