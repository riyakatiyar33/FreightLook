namespace Models.Dtos;

public class SurchargeDto
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? MinAmount { get; set; }

    /// <summary>"KG" (per chargeable kg) or "SHIPMENT" (flat).</summary>
    public string Basis { get; set; } = string.Empty;
}
