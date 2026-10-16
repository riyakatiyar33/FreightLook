namespace Models.Dtos;

public class RateSearchResultDto
{
    public int RateId { get; set; }
    public string AirlineCode { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public string OriginCode { get; set; } = string.Empty;
    public string OriginName { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;

    /// <summary>Special commodity / product (Courier, DGR, ...). Empty = general cargo.</summary>
    public string Commodity { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal MinimumCharge { get; set; }

    /// <summary>Price per kg keyed by slab code (M, +45, +100, ...). Missing slabs are absent.</summary>
    public Dictionary<string, decimal> Slabs { get; set; } = [];

    public List<SurchargeDto> Surcharges { get; set; } = [];

    /// <summary>Slab used for the given weight; null when no weight was supplied.</summary>
    public string? ApplicableSlab { get; set; }

    /// <summary>Freight (slab price x weight, at least the minimum charge); null without weight.</summary>
    public decimal? FreightCharge { get; set; }

    /// <summary>Sum of all surcharges for the weight; null without weight.</summary>
    public decimal? SurchargeTotal { get; set; }

    /// <summary>FreightCharge + SurchargeTotal; null without weight.</summary>
    public decimal? EstimatedTotal { get; set; }

    public DateTime UpdatedAt { get; set; }
}
