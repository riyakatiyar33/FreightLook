namespace Models.Dtos;

/// <summary>
/// A rate sheet flattened to text cells, independent of the file format.
/// Readers (PDF, Excel, CSV) produce this; the rate sheet parser consumes it.
/// </summary>
public class RawSheetDto
{
    public List<RawSheetRow> Rows { get; set; } = [];
}

public class RawSheetRow
{
    /// <summary>Location in the file for error messages (e.g. "Page 1, line 4" or "Sheet1, row 7").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Cell texts left to right. Empty cells are empty strings.</summary>
    public List<string> Cells { get; set; } = [];
}
