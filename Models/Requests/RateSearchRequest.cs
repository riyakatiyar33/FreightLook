using System.ComponentModel.DataAnnotations;

namespace Models.Requests;

public class RateSearchRequest
{
    [Required]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Origin must be a 3-letter IATA airport code.")]
    public string Origin { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Destination must be a 3-letter IATA airport code.")]
    public string Destination { get; set; } = string.Empty;

    /// <summary>Chargeable weight in kg. Optional.</summary>
    [Range(0.01, 1_000_000, ErrorMessage = "Weight must be between 0.01 and 1,000,000 kg.")]
    public decimal? Weight { get; set; }
}
