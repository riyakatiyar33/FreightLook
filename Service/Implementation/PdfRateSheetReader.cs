using System.ComponentModel.DataAnnotations;
using Models.Dtos;
using Service.Interface;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Service.Implementation;

/// <summary>
/// Rebuilds the rate table from a text-based PDF using word positions (PdfPig):
/// 1. words are grouped into visual lines;
/// 2. the header line (Airline, Normal, +45 ...) gives the column positions;
/// 3. every line with an airport code in the first (Loc) column starts a table row, and wrapped
///    cell text above/below it (e.g. "4.000c/" + "300.000") is merged into that row.
/// Image-only (scanned) PDFs have no words and are rejected with a clear message.
/// </summary>
public sealed class PdfRateSheetReader : IRateSheetReader
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".pdf"];

    public RawSheetDto Read(byte[] content, string fileName)
    {
        var sheet = new RawSheetDto();
        var hasText = false;
        List<Column>? columns = null;

        using var document = PdfDocument.Open(content);
        foreach (var page in document.GetPages())
        {
            // Keep punctuation words (":" and "/" matter), but a page counts as text only if it has letters/digits.
            var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
            if (!words.Any(w => w.Text.Any(char.IsLetterOrDigit)))
                continue;

            hasText = true;
            var lineHeight = Median(words.Select(w => w.BoundingBox.Height));
            var lines = GroupIntoLines(words, lineHeight);
            var source = $"Page {page.Number}";

            var start = 0;
            while (start < lines.Count)
            {
                var headerIndex = FindHeaderLine(lines, start);
                var header = headerIndex < 0 ? default : BuildColumns(lines, headerIndex, lineHeight);
                var end = headerIndex < 0 ? lines.Count : Math.Max(start, header.From);

                // Lines before the next header block belong to the current table (if any) or are free text.
                if (columns is not null)
                    AddTableRows(lines[start..end], columns, lineHeight, source, sheet);
                else
                    lines[start..end].ForEach(line => AddTextRow(line, source, sheet));

                if (headerIndex < 0)
                    break;

                columns = header.Columns;
                sheet.Rows.Add(new RawSheetRow { Source = source, Cells = columns.Select(c => c.Text).ToList() });
                start = header.To + 1;
            }
        }

        if (!hasText)
            throw new ValidationException(
                "This PDF has no selectable text (it is a scanned or image-only PDF). " +
                "Export the rate sheet from the source system as a text PDF or as Excel/CSV and upload that.");

        return sheet;
    }

    private static List<Line> GroupIntoLines(List<Word> words, double lineHeight)
    {
        var lines = new List<Line>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Centroid.Y))
        {
            var y = word.BoundingBox.Centroid.Y;
            var line = lines.Count > 0 && Math.Abs(lines[^1].Y - y) <= lineHeight * 0.5 ? lines[^1] : null;
            if (line is null)
            {
                line = new Line(y);
                lines.Add(line);
            }
            line.Words.Add(word);
        }

        lines.ForEach(l => l.Words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left)));
        return lines;
    }

    private static int FindHeaderLine(List<Line> lines, int start)
    {
        for (var i = start; i < lines.Count; i++)
        {
            var cells = MergeIntoCells(lines[i].Words, Median(lines[i].Words.Select(w => w.BoundingBox.Height)));
            if (RateSheetParser.TryMapHeader(cells.Select(c => c.Text).ToList()) is not null)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Builds the column layout from the header line. Multi-line headers ("Ams-" over "M/Awb") are
    /// handled by trying the neighbouring lines and keeping them only when they add recognised headers
    /// without breaking any (so a title line above or the first data line below is never absorbed).
    /// </summary>
    private static (List<Column> Columns, int From, int To) BuildColumns(List<Line> lines, int headerIndex, double lineHeight)
    {
        // Header rows can be tall (padding around two-line labels), so look up to ~2.5 text heights away.
        bool IsNear(int i) => i >= 0 && i < lines.Count && Math.Abs(lines[i].Y - lines[headerIndex].Y) <= lineHeight * 2.5;

        var best = (Cells: MergeIntoCells(lines[headerIndex].Words, lineHeight), From: headerIndex, To: headerIndex);
        var bestKeys = RecognisedKeys(best.Cells);

        foreach (var (from, to) in new[] { (headerIndex - 1, headerIndex), (headerIndex, headerIndex + 1), (headerIndex - 1, headerIndex + 1) })
        {
            if (!IsNear(from) || !IsNear(to))
                continue;

            var cells = MergeIntoCells(lines[from..(to + 1)].SelectMany(l => l.Words).ToList(), lineHeight);
            var keys = RecognisedKeys(cells);
            if (keys.Count > bestKeys.Count && bestKeys.IsSubsetOf(keys))
                (best, bestKeys) = ((cells, from, to), keys);
        }

        var columns = best.Cells;

        // Column boundaries sit halfway between neighbouring header texts.
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].MinX = i == 0 ? double.MinValue : (columns[i - 1].Right + columns[i].Left) / 2;
            columns[i].MaxX = i == columns.Count - 1 ? double.MaxValue : (columns[i].Right + columns[i + 1].Left) / 2;
        }
        return (columns, best.From, best.To);
    }

    private static HashSet<string> RecognisedKeys(List<Column> cells) =>
        cells.Select(c => RateSheetParser.HeaderKey(c.Text)).OfType<string>().ToHashSet();

    /// <summary>
    /// Merges words that overlap horizontally or are separated by about one space into cells.
    /// Narrow neighbouring headers can sit closer than a space ("M/Awb" | "M/Hawb"); an unrecognised
    /// merged cell is therefore split on true overlap when that yields recognised headers.
    /// </summary>
    private static List<Column> MergeIntoCells(List<Word> words, double lineHeight)
    {
        var result = new List<Column>();
        foreach (var cell in MergeByGap(words, lineHeight * 0.45))
        {
            var parts = cell.Words.Count > 1 && RateSheetParser.HeaderKey(cell.Text) is null
                ? MergeByGap(cell.Words, 0)
                : null;

            if (parts is { Count: > 1 } && parts.All(p => RateSheetParser.HeaderKey(p.Text) is not null))
                result.AddRange(parts);
            else
                result.Add(cell);
        }
        return result;
    }

    private static List<Column> MergeByGap(List<Word> words, double maxGap)
    {
        var lineHeight = Median(words.Select(w => w.BoundingBox.Height));
        var cells = new List<Column>();
        foreach (var word in words.OrderBy(w => w.BoundingBox.Left))
        {
            var cell = cells.Count > 0 && word.BoundingBox.Left <= cells[^1].Right + maxGap ? cells[^1] : null;
            if (cell is null)
            {
                cell = new Column { Left = word.BoundingBox.Left, Right = word.BoundingBox.Right };
                cells.Add(cell);
            }
            cell.Right = Math.Max(cell.Right, word.BoundingBox.Right);
            cell.Words.Add(word);
        }

        foreach (var cell in cells)
            cell.Text = string.Join(" ", cell.Words
                .OrderByDescending(w => Math.Round(w.BoundingBox.Centroid.Y / lineHeight))
                .ThenBy(w => w.BoundingBox.Left)
                .Select(w => w.Text));
        return cells;
    }

    private static void AddTableRows(List<Line> lines, List<Column> columns, double lineHeight, string source, RawSheetDto sheet)
    {
        if (lines.Count == 0)
            return;

        // A row starts at a line with an airport code (e.g. "DEL") in the origin (Loc) column.
        // Without an origin column, every line is its own row.
        var originIndex = columns.FindIndex(c => RateSheetParser.HeaderKey(c.Text) == RateSheetParser.Origin);
        var anchors = originIndex < 0
            ? lines
            : lines.Where(line => line.Words.Any(w =>
                    ColumnIndex(w, columns) == originIndex && w.Text.Length == 3 && w.Text.All(char.IsAsciiLetterUpper)))
                .ToList();

        var spacing = anchors.Count > 1
            ? Median(anchors.Zip(anchors.Skip(1), (a, b) => a.Y - b.Y))
            : lineHeight * 2.5;
        var maxDistance = Math.Max(spacing * 0.75, lineHeight * 0.9);

        var rows = anchors.ToDictionary(a => a, a => new List<Line> { a });
        foreach (var line in lines.Where(l => !rows.ContainsKey(l)))
        {
            var nearest = anchors.MinBy(a => Math.Abs(a.Y - line.Y));
            if (nearest is not null && Math.Abs(nearest.Y - line.Y) <= maxDistance)
                rows[nearest].Add(line);
            else
                AddTextRow(line, source, sheet); // title, footer or a new "Destination :" line
        }

        foreach (var anchor in anchors)
        {
            var cells = Enumerable.Range(0, columns.Count).Select(_ => new List<string>()).ToList();
            foreach (var word in rows[anchor].OrderByDescending(l => l.Y).SelectMany(l => l.Words))
                cells[ColumnIndex(word, columns)].Add(word.Text);

            var lineNumber = sheet.Rows.Count(r => r.Source.StartsWith(source + ", line")) + 1;
            sheet.Rows.Add(new RawSheetRow
            {
                Source = $"{source}, line {lineNumber}",
                Cells = cells.Select(c => string.Join(" ", c)).ToList()
            });
        }
    }

    /// <summary>
    /// Text that starts with a letter is left-aligned and may overflow to the right ("SRILANKAN AIRLINES"),
    /// so it is placed by its left edge. Everything else (amounts, "607/EY" codes) goes to the column it
    /// overlaps most, which works for left-aligned, right-aligned and centred columns alike.
    /// </summary>
    private static int ColumnIndex(Word word, List<Column> columns)
    {
        var (left, right) = (word.BoundingBox.Left, word.BoundingBox.Right);
        if (char.IsLetter(word.Text[0]))
        {
            var index = columns.FindIndex(c => left + 0.01 >= c.MinX && left + 0.01 < c.MaxX);
            if (index >= 0)
                return index;
        }

        var best = -1;
        var bestOverlap = double.MinValue;
        for (var i = 0; i < columns.Count; i++)
        {
            // Negative when the word is outside the column: the closest column still wins.
            var overlap = Math.Min(right, columns[i].MaxX) - Math.Max(left, columns[i].MinX);
            if (overlap > bestOverlap)
                (best, bestOverlap) = (i, overlap);
        }
        return best;
    }

    private static void AddTextRow(Line line, string source, RawSheetDto sheet) =>
        sheet.Rows.Add(new RawSheetRow { Source = source, Cells = [string.Join(" ", line.Words.Select(w => w.Text))] });

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }

    private sealed class Line(double y)
    {
        public double Y { get; } = y;
        public List<Word> Words { get; } = [];
    }

    private sealed class Column
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<Word> Words { get; } = [];
    }
}
