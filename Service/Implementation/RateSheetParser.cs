using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Models.Constants;
using Models.Dtos;

namespace Service.Implementation;

/// <summary>
/// Turns a <see cref="RawSheetDto"/> (rows of text cells) into rate rows. Columns are found by
/// header name, so the same parser handles PDF and Excel/CSV exports, and column order does not matter.
/// Sheet-level values (destination, currency) are read from lines like "Destination : DOHA (DOH)".
/// </summary>
internal static partial class RateSheetParser
{
    // Column keys that are not slab codes or surcharge types.
    internal const string Origin = "origin";
    internal const string Destination = "destination";
    internal const string Airline = "airline";
    internal const string AirlineCode = "airlineCode";
    internal const string Commodity = "commodity";
    internal const string Currency = "currency";
    internal const string MinimumCharge = "minimumCharge";
    internal const string AmsElectronicAwb = "amsElectronicAwb";
    internal const string AmsManualAwb = "amsManualAwb";
    internal const string Ignored = "ignored";

    /// <summary>Normalized header text (lower case, no whitespace, no trailing "/min") to column key.</summary>
    private static readonly Dictionary<string, string> HeaderAliases = new()
    {
        ["loc"] = Origin, ["origin"] = Origin, ["from"] = Origin, ["org"] = Origin,
        ["dest"] = Destination, ["destination"] = Destination, ["to"] = Destination, ["dst"] = Destination,
        ["airline"] = Airline, ["airlinename"] = Airline, ["carrier"] = Airline,
        ["cd"] = AirlineCode, ["code"] = AirlineCode, ["airlinecode"] = AirlineCode, ["carriercode"] = AirlineCode,
        ["scr"] = Commodity, ["scc"] = Commodity, ["commodity"] = Commodity, ["product"] = Commodity,
        ["crny"] = Currency, ["currency"] = Currency, ["curr"] = Currency, ["cur"] = Currency, ["ccy"] = Currency,
        ["min"] = MinimumCharge, ["minimum"] = MinimumCharge, ["mincharge"] = MinimumCharge, ["minimumcharge"] = MinimumCharge,
        ["normal"] = SlabCodes.Minimum, ["n"] = SlabCodes.Minimum, ["m"] = SlabCodes.Minimum, ["-45"] = SlabCodes.Minimum, ["<45"] = SlabCodes.Minimum,
        ["+45"] = SlabCodes.Plus45, ["45"] = SlabCodes.Plus45,
        ["+100"] = SlabCodes.Plus100, ["100"] = SlabCodes.Plus100,
        ["+250"] = SlabCodes.Plus250, ["250"] = SlabCodes.Plus250,
        ["+300"] = SlabCodes.Plus300, ["300"] = SlabCodes.Plus300,
        ["+500"] = SlabCodes.Plus500, ["500"] = SlabCodes.Plus500,
        ["+1000"] = SlabCodes.Plus1000, ["1000"] = SlabCodes.Plus1000,
        ["fsc"] = SurchargeTypes.Fuel, ["fuel"] = SurchargeTypes.Fuel,
        ["ssc"] = SurchargeTypes.Security, ["sec"] = SurchargeTypes.SecurityAlt,
        ["xray"] = SurchargeTypes.XRay, ["x-ray"] = SurchargeTypes.XRay,
        ["misc"] = SurchargeTypes.Misc,
        ["ctg"] = SurchargeTypes.Cartage, ["cartage"] = SurchargeTypes.Cartage,
        ["awb"] = SurchargeTypes.Awb, ["awbfee"] = SurchargeTypes.Awb,
        ["ams"] = SurchargeTypes.Ams,
        ["ams-e/awb"] = AmsElectronicAwb, ["amse/awb"] = AmsElectronicAwb,
        ["ams-m/awb"] = AmsManualAwb, ["amsm/awb"] = AmsManualAwb,
        ["ams-e/hawb"] = Ignored, ["ams-m/hawb"] = Ignored, ["amse/hawb"] = Ignored, ["amsm/hawb"] = Ignored,
        ["shpmttype"] = Ignored, ["shpmnttype"] = Ignored, [".."] = Ignored, ["#"] = Ignored, ["sno"] = Ignored, ["srno"] = Ignored,
    };

    private static readonly string[] SurchargeColumns =
    [
        SurchargeTypes.Fuel, SurchargeTypes.Security, SurchargeTypes.SecurityAlt,
        SurchargeTypes.XRay, SurchargeTypes.Misc, SurchargeTypes.Cartage, SurchargeTypes.Awb
    ];

    /// <summary>Column key for a header text, or null when it is not a known header.</summary>
    internal static string? HeaderKey(string text) =>
        HeaderAliases.TryGetValue(NormalizeHeader(text), out var key) ? key : null;

    /// <summary>Maps cell index to column key when the cells look like the rate table header row.</summary>
    internal static Dictionary<string, int>? TryMapHeader(IReadOnlyList<string> cells)
    {
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < cells.Count; i++)
        {
            var key = HeaderKey(cells[i]);
            if (key is not null && key != Ignored)
                columns.TryAdd(key, i);
        }

        var hasAirline = columns.ContainsKey(Airline) || columns.ContainsKey(AirlineCode);
        var hasSlab = SlabCodes.All.Any(columns.ContainsKey);
        return hasAirline && hasSlab && columns.Count >= 4 ? columns : null;
    }

    public static ExtractionResultDto Parse(RawSheetDto sheet)
    {
        var result = new ExtractionResultDto();
        Dictionary<string, int>? columns = null;
        string? sheetOrigin = null, sheetDestination = null, sheetCurrency = null;

        foreach (var row in sheet.Rows)
        {
            if (TryMapHeader(row.Cells) is { } header)
            {
                columns = header;
                continue;
            }

            if (columns is not null && IsDataRow(row, columns))
            {
                ParseDataRow(row, columns, sheetOrigin, sheetDestination, sheetCurrency, result);
                continue;
            }

            // Sheet-level metadata such as "Destination : DOHA (DOH)" or "Currency : INR (1)".
            var text = string.Join(" ", row.Cells.Where(c => c.Length > 0));
            sheetDestination = MatchAirport(DestinationRegex(), text) ?? sheetDestination;
            sheetOrigin = MatchAirport(OriginRegex(), text) ?? sheetOrigin;
            if (CurrencyRegex().Match(text) is { Success: true } currency)
                sheetCurrency = currency.Groups[1].Value.ToUpperInvariant();
        }

        if (columns is null)
            throw new ValidationException(
                "Could not find the rate table header. Expected columns such as Loc, Airline, Cd, Crny, Min., Normal, +45, +100 ...");

        return result;
    }

    private static bool IsDataRow(RawSheetRow row, Dictionary<string, int> columns)
    {
        // With an origin column every rate row starts with an airport code (e.g. "DEL"); this skips footers.
        if (columns.ContainsKey(Origin))
            return AirportCodeRegex().IsMatch(Cell(row, columns, Origin));

        return Cell(row, columns, Airline).Length > 0 || Cell(row, columns, AirlineCode).Length > 0;
    }

    private static void ParseDataRow(
        RawSheetRow row, Dictionary<string, int> columns,
        string? sheetOrigin, string? sheetDestination, string? sheetCurrency,
        ExtractionResultDto result)
    {
        var airlineName = Cell(row, columns, Airline);
        var commodity = Cell(row, columns, Commodity);
        var currency = NullIfEmpty(Cell(row, columns, Currency))?.ToUpperInvariant() ?? sheetCurrency;
        var label = $"{row.Source} ({string.Join(" ", new[] { airlineName, commodity }.Where(s => s.Length > 0))})";

        if (commodity.Replace(" ", "").Contains("callcar", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"{label}: skipped, rates on request (call carrier).");
            return;
        }

        var slabs = new Dictionary<string, decimal?>();
        foreach (var code in SlabCodes.All.Where(columns.ContainsKey))
            slabs[code] = ParseNumber(Cell(row, columns, code));

        var minimumCharge = ParseNumber(Cell(row, columns, MinimumCharge));
        if (minimumCharge is null && slabs.Values.All(v => v is null))
        {
            result.Warnings.Add($"{label}: skipped, no rates in the row.");
            return;
        }

        var surcharges = new List<ExtractedSurchargeDto>();
        foreach (var type in SurchargeColumns.Where(columns.ContainsKey))
        {
            var text = Cell(row, columns, type);
            if (text.Length > 0 && ParseSurcharge(text, type, currency, label, result.Warnings) is { } surcharge)
                surcharges.Add(surcharge);
        }

        // AMS is a flat fee per AWB: prefer the electronic (E-AWB) amount, then the manual one.
        // Values like "15 /usd" (another currency) are reported as warnings and skipped.
        var amsText = new[] { AmsElectronicAwb, AmsManualAwb, SurchargeTypes.Ams }
            .Select(key => Cell(row, columns, key))
            .FirstOrDefault(text => text.Length > 0);
        if (amsText is not null && ParseSurcharge(amsText, SurchargeTypes.Ams, currency, label, result.Warnings) is { } ams)
        {
            ams.MinAmount = null;
            ams.Basis = SurchargeBasis.PerShipment;
            surcharges.Add(ams);
        }

        result.Rates.Add(new ExtractedRateDto
        {
            Source = row.Source,
            AirlineCode = ParseAirlineCode(Cell(row, columns, AirlineCode)),
            AirlineName = NullIfEmpty(airlineName),
            Origin = NullIfEmpty(Cell(row, columns, Origin)) ?? sheetOrigin,
            Destination = NullIfEmpty(Cell(row, columns, Destination)) ?? sheetDestination,
            Commodity = commodity,
            Currency = currency,
            MinimumCharge = minimumCharge,
            Slabs = slabs,
            Surcharges = surcharges
        });
    }

    /// <summary>
    /// Parses surcharge cells such as "127.000c", "100.000g", "4.000c/ 300.000" (per kg, minimum 300) or "20.000/2500.000".
    /// Gross-weight ("g") charges are estimated on the searched weight, since only one weight is entered.
    /// "0.250c /usd" (a different currency) is skipped with a warning.
    /// </summary>
    private static ExtractedSurchargeDto? ParseSurcharge(string text, string type, string? currency, string label, List<string> warnings)
    {
        var match = SurchargeRegex().Match(WhitespaceRegex().Replace(text, ""));
        if (!match.Success)
        {
            warnings.Add($"{label}: {type} value '{text}' could not be read; ignored.");
            return null;
        }

        var otherCurrency = match.Groups["currency"].Value.ToUpperInvariant();
        if (otherCurrency.Length > 0 && otherCurrency != currency)
        {
            warnings.Add($"{label}: {type} '{text}' is in {otherCurrency}, not {currency ?? "the sheet currency"}; ignored.");
            return null;
        }

        return new ExtractedSurchargeDto
        {
            Type = type,
            Amount = ParseNumber(match.Groups["amount"].Value),
            MinAmount = ParseNumber(match.Groups["min"].Value)
        };
    }

    /// <summary>"098/AI" -> "AI" (AWB prefix / IATA code); "EK" -> "EK"; digits only -> null.</summary>
    private static string? ParseAirlineCode(string text)
    {
        var code = text.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return code is null || code.All(char.IsDigit) ? null : code.ToUpperInvariant();
    }

    private static decimal? ParseNumber(string text)
    {
        text = text.Replace(",", "").Replace(" ", "");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string Cell(RawSheetRow row, Dictionary<string, int> columns, string key) =>
        columns.TryGetValue(key, out var index) && index < row.Cells.Count ? row.Cells[index].Trim() : string.Empty;

    private static string NormalizeHeader(string text)
    {
        var header = WhitespaceRegex().Replace(text, "").ToLowerInvariant().TrimEnd('.', ':');
        return header.EndsWith("/min") ? header[..^4] : header;
    }

    private static string? MatchAirport(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups["code"].Value.ToUpperInvariant() : null;
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^[A-Za-z]{3}$")]
    private static partial Regex AirportCodeRegex();

    [GeneratedRegex(@"Destination\s*:\s*(?:[^()]*\(\s*(?<code>[A-Za-z]{3})\s*\)|(?<code>[A-Za-z]{3})\b)", RegexOptions.IgnoreCase)]
    private static partial Regex DestinationRegex();

    [GeneratedRegex(@"Origin\s*:\s*(?:[^()]*\(\s*(?<code>[A-Za-z]{3})\s*\)|(?<code>[A-Za-z]{3})\b)", RegexOptions.IgnoreCase)]
    private static partial Regex OriginRegex();

    [GeneratedRegex(@"Currency\s*:\s*([A-Za-z]{3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyRegex();

    // Suffix "c" = per kg of chargeable weight, "g" = per kg of gross weight (as printed in the sheet legend).
    [GeneratedRegex(@"^(?<amount>\d[\d,]*(?:\.\d+)?)[cg]?(?:/(?:(?<min>\d[\d,]*(?:\.\d+)?)|(?<currency>[A-Za-z]{3})))?$", RegexOptions.IgnoreCase)]
    private static partial Regex SurchargeRegex();
}
