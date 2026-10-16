namespace Models.Entities;

public class RateSlab
{
    public int RateSlabId { get; set; }
    public int RateId { get; set; }

    /// <summary>One of <see cref="Constants.SlabCodes"/>.</summary>
    public string SlabCode { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
}
