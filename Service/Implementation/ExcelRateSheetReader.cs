using System.Globalization;
using System.Text;
using ExcelDataReader;
using Models.Dtos;
using Service.Interface;

namespace Service.Implementation;

/// <summary>Reads .xlsx, .xls and .csv exports into rows of text cells (all worksheets, top to bottom).</summary>
public sealed class ExcelRateSheetReader : IRateSheetReader
{
    static ExcelRateSheetReader()
    {
        // Legacy .xls and CSV files may use Windows code pages.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xlsx", ".xls", ".csv"];

    public RawSheetDto Read(byte[] content, string fileName)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);

        var sheet = new RawSheetDto();
        do
        {
            var rowNumber = 0;
            while (reader.Read())
            {
                rowNumber++;
                var cells = new List<string>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    cells.Add(ToText(reader.GetValue(i)));

                if (cells.All(c => c.Length == 0))
                    continue;

                sheet.Rows.Add(new RawSheetRow { Source = $"{reader.Name}, row {rowNumber}", Cells = cells });
            }
        } while (reader.NextResult());

        return sheet;
    }

    private static string ToText(object? value) => value switch
    {
        null => string.Empty,
        double d => d.ToString(CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };
}
