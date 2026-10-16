using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Dapper;
using Data;
using Models.Constants;
using Models.Dtos;
using Models.Entities;
using Models.Responses;
using Service.Interface;

namespace Service.Implementation;

public sealed partial class UploadService : IUploadService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Supported extensions and the leading bytes ("magic number") a real file of that type starts with.</summary>
    private static readonly IReadOnlyDictionary<string, byte[]?> AllowedTypes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "%PDF"u8.ToArray(),
        [".xlsx"] = [0x50, 0x4B, 0x03, 0x04],              // ZIP container
        [".xls"] = [0xD0, 0xCF, 0x11, 0xE0],               // OLE2 container
        [".csv"] = null,
    };

    private readonly IExtractionService _extractionService;
    private readonly IDbConnectionFactory _connectionFactory;

    public UploadService(IExtractionService extractionService, IDbConnectionFactory connectionFactory)
    {
        _extractionService = extractionService;
        _connectionFactory = connectionFactory;
    }

    public async Task<UploadResultResponse> UploadAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        fileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ValidationException("File name is missing.");
        if (length <= 0)
            throw new ValidationException("The file is empty.");
        if (length > MaxFileBytes)
            throw new ValidationException($"The file is larger than {MaxFileBytes / (1024 * 1024)} MB.");
        if (!AllowedTypes.TryGetValue(Path.GetExtension(fileName), out var signature))
            throw new ValidationException("Only PDF (text-based), XLSX, XLS and CSV files are supported.");

        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await content.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        if (signature is not null && !bytes.AsSpan().StartsWith(signature))
            throw new ValidationException($"The file content does not match its '{Path.GetExtension(fileName)}' extension.");

        var extraction = await _extractionService.ExtractAsync(bytes, fileName, contentType, cancellationToken);

        var response = new UploadResultResponse
        {
            FileName = fileName,
            Parsed = extraction.Rates.Count,
            Warnings = extraction.Warnings
        };
        if (extraction.Rates.Count == 0)
            response.Errors.Add("No rates could be found in the file.");

        // One connection for the whole upload; each rate is still saved in its own transaction (inside the SP).
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        for (var i = 0; i < extraction.Rates.Count; i++)
        {
            var row = extraction.Rates[i];
            var label = $"{row.Source ?? $"Row {i + 1}"} ({Describe(row)})";

            var errors = TryMap(row, fileName, out var rate);
            if (errors.Count > 0)
            {
                response.Rejected++;
                response.Errors.Add($"{label}: {string.Join("; ", errors)}");
                continue;
            }

            try
            {
                await InsertRateAsync(connection, rate!, cancellationToken);
                response.Saved++;
                response.SavedRoutes.Add(Describe(row));
            }
            catch (DbException ex)
            {
                response.Rejected++;
                response.Errors.Add($"{label}: could not be saved ({ex.Message}).");
            }
        }

        await InsertUploadLogAsync(connection, new RateSheetUpload
        {
            FileName = fileName,
            ParsedCount = response.Parsed,
            SavedCount = response.Saved,
            RejectedCount = response.Rejected
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// usp_Rate_Insert: saves the rate with its slabs and surcharges (passed as table-valued parameters)
    /// and deactivates the previous rate for the same airline / route / commodity. Returns the new RateId.
    /// </summary>
    private static async Task<int> InsertRateAsync(DbConnection connection, Rate rate, CancellationToken cancellationToken)
    {
        var slabs = new DataTable();
        slabs.Columns.Add("SlabCode", typeof(string));
        slabs.Columns.Add("PricePerKg", typeof(decimal));
        foreach (var slab in rate.Slabs)
            slabs.Rows.Add(slab.SlabCode, slab.PricePerKg);

        var surcharges = new DataTable();
        surcharges.Columns.Add("SurchargeType", typeof(string));
        surcharges.Columns.Add("Amount", typeof(decimal));
        surcharges.Columns.Add("MinAmount", typeof(decimal));
        surcharges.Columns.Add("Basis", typeof(string));
        foreach (var surcharge in rate.Surcharges)
            surcharges.Rows.Add(surcharge.SurchargeType, surcharge.Amount, (object?)surcharge.MinAmount ?? DBNull.Value, surcharge.Basis);

        var parameters = new DynamicParameters();
        parameters.Add("@AirlineCode", rate.AirlineCode);
        parameters.Add("@AirlineName", string.IsNullOrWhiteSpace(rate.AirlineName) ? null : rate.AirlineName);
        parameters.Add("@OriginCode", rate.OriginCode);
        parameters.Add("@DestinationCode", rate.DestinationCode);
        parameters.Add("@Commodity", rate.Commodity);
        parameters.Add("@Currency", rate.Currency);
        parameters.Add("@MinimumCharge", rate.MinimumCharge);
        parameters.Add("@SourceFile", rate.SourceFile);
        parameters.Add("@Slabs", slabs.AsTableValuedParameter("dbo.RateSlabTableType"));
        parameters.Add("@Surcharges", surcharges.AsTableValuedParameter("dbo.SurchargeTableType"));
        parameters.Add("@RateId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Rate_Insert", parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<int>("@RateId");
    }

    /// <summary>usp_RateSheetUpload_Insert: logs the upload (feeds the dashboard "last upload" card).</summary>
    private static Task<int> InsertUploadLogAsync(DbConnection connection, RateSheetUpload upload, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "dbo.usp_RateSheetUpload_Insert",
            new { upload.FileName, upload.ParsedCount, upload.SavedCount, upload.RejectedCount },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

    /// <summary>Validates one extracted row and maps it to a <see cref="Rate"/>. Returns the list of problems.</summary>
    private static List<string> TryMap(ExtractedRateDto row, string fileName, out Rate? rate)
    {
        rate = null;
        var errors = new List<string>();

        var airline = Clean(row.AirlineCode);
        var origin = Clean(row.Origin);
        var destination = Clean(row.Destination);
        var currency = Clean(row.Currency) ?? "USD";
        var commodity = row.Commodity?.Trim() ?? string.Empty;

        if (airline is null || !AirlineCodeRegex().IsMatch(airline))
            errors.Add("airline code must be 2-3 letters/digits");
        if (origin is null || !AirportCodeRegex().IsMatch(origin))
            errors.Add("origin must be a 3-letter airport code");
        if (destination is null || !AirportCodeRegex().IsMatch(destination))
            errors.Add("destination must be a 3-letter airport code");
        if (origin is not null && origin == destination)
            errors.Add("origin and destination are the same");
        if (!CurrencyRegex().IsMatch(currency))
            errors.Add($"invalid currency '{currency}'");
        if (commodity.Length > 30)
            errors.Add("commodity is longer than 30 characters");
        if (row.MinimumCharge is null)
            errors.Add("minimum charge is missing");
        else if (row.MinimumCharge < 0)
            errors.Add("minimum charge is negative");

        var slabs = new List<RateSlab>();
        foreach (var (rawCode, price) in row.Slabs ?? [])
        {
            if (price is null)
                continue;

            var code = NormalizeSlabCode(rawCode);
            if (code is null)
                errors.Add($"unknown slab '{rawCode}'");
            else if (price < 0)
                errors.Add($"slab {code} price is negative");
            else if (slabs.Any(s => s.SlabCode == code))
                errors.Add($"slab {code} appears twice");
            else
                slabs.Add(new RateSlab { SlabCode = code, PricePerKg = price.Value });
        }
        if (slabs.Count == 0)
            errors.Add("no slab prices");

        var surcharges = new List<Surcharge>();
        foreach (var item in row.Surcharges ?? [])
        {
            var type = Clean(item.Type)?.Replace("-", "").Replace(" ", "");
            if (type == "XRY") type = SurchargeTypes.XRay;

            if (type is null || !SurchargeTypes.All.Contains(type))
                errors.Add($"unknown surcharge '{item.Type}'");
            else if (item.Amount is null)
                continue;
            else if (item.Amount < 0 || item.MinAmount < 0)
                errors.Add($"surcharge {type} is negative");
            else if (surcharges.Any(s => s.SurchargeType == type))
                errors.Add($"surcharge {type} appears twice");
            else
                surcharges.Add(new Surcharge
                {
                    SurchargeType = type,
                    Amount = item.Amount.Value,
                    MinAmount = item.MinAmount,
                    Basis = NormalizeBasis(item.Basis) ?? DefaultBasis(type)
                });
        }

        if (errors.Count > 0)
            return errors;

        rate = new Rate
        {
            AirlineCode = airline!,
            AirlineName = row.AirlineName?.Trim() ?? string.Empty,
            OriginCode = origin!,
            DestinationCode = destination!,
            Commodity = commodity,
            Currency = currency,
            MinimumCharge = row.MinimumCharge!.Value,
            SourceFile = fileName,
            Slabs = slabs,
            Surcharges = surcharges
        };
        return errors;
    }

    private static string? NormalizeSlabCode(string? raw)
    {
        var code = Clean(raw)?.Replace(" ", "").Replace(",", "").Replace("KG", "");
        if (code is "M" or "MIN" or "MINIMUM" or "N" or "NORMAL" or "-45" or "<45")
            return SlabCodes.Minimum;
        if (code is not null && !code.StartsWith('+') && decimal.TryParse(code, out _))
            code = "+" + code;
        return code is not null && SlabCodes.All.Contains(code) ? code : null;
    }

    private static string? NormalizeBasis(string? raw) => Clean(raw) switch
    {
        "KG" or "PERKG" or "PER KG" or "/KG" => SurchargeBasis.PerKg,
        "SHIPMENT" or "PERSHIPMENT" or "PER SHIPMENT" or "FLAT" or "AWB" or "PERAWB" => SurchargeBasis.PerShipment,
        _ => null
    };

    /// <summary>Industry default: FSC/SSC/SEC/XRAY/MISC/CTG per kg; AMS/AWB flat per shipment.</summary>
    private static string DefaultBasis(string type) =>
        type is SurchargeTypes.Ams or SurchargeTypes.Awb
            ? SurchargeBasis.PerShipment
            : SurchargeBasis.PerKg;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string Describe(ExtractedRateDto row) =>
        $"{row.AirlineCode ?? row.AirlineName ?? "?"} {row.Origin ?? "?"}-{row.Destination ?? "?"}" +
        (string.IsNullOrWhiteSpace(row.Commodity) ? "" : $" {row.Commodity.Trim()}");

    [GeneratedRegex("^[A-Z0-9]{2,3}$")]
    private static partial Regex AirlineCodeRegex();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex AirportCodeRegex();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyRegex();
}
