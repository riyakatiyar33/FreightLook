namespace Models.Entities;

public class Surcharge
{
    public int SurchargeId { get; set; }
    public int RateId { get; set; }

    /// <summary>One of <see cref="Constants.SurchargeTypes"/>.</summary>
    public string SurchargeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Minimum charge for a per-kg surcharge (e.g. "4.000c / 300.000"). Null = no minimum.</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>One of <see cref="Constants.SurchargeBasis"/>.</summary>
    public string Basis { get; set; } = Constants.SurchargeBasis.PerKg;
}
