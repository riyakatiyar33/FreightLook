namespace Models.Dtos;

/// <summary>
/// One rate row read from a rate sheet, before validation.
/// Everything is nullable because the source file is untrusted.
/// </summary>
public class ExtractedRateDto
{
    /// <summary>Where the row came from, for error messages (e.g. "Page 1, line 3").</summary>
    public string? Source { get; set; }

    public string? AirlineCode { get; set; }
    public string? AirlineName { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? Commodity { get; set; }
    public string? Currency { get; set; }
    public decimal? MinimumCharge { get; set; }

    /// <summary>Price per kg keyed by slab code (M, +45, +100, +250, +300, +500, +1000).</summary>
    public Dictionary<string, decimal?>? Slabs { get; set; }

    public List<ExtractedSurchargeDto>? Surcharges { get; set; }
}

public class ExtractedSurchargeDto
{
    public string? Type { get; set; }
    public decimal? Amount { get; set; }
    public decimal? MinAmount { get; set; }
    public string? Basis { get; set; }
}

public class ExtractionResultDto
{
    public List<ExtractedRateDto> Rates { get; set; } = [];

    /// <summary>Non-fatal notes, e.g. rows skipped because the carrier must be called.</summary>
    public List<string> Warnings { get; set; } = [];
}
