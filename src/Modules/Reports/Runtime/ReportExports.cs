using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Reports;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>A file of a report: its name, media type and content.</summary>
public sealed record ReportFile(string FileName, string ContentType, byte[] Content);

/// <summary>Writes a report's rows as Excel (a title, the filters, the columns, the totals row; right to left in Arabic; its charts on a sheet of their own) or CSV.</summary>
internal static class ReportExports
{
    public const int MaxRows = 50_000;

    /// <summary>The report as a file; an Excel file also holds the report's charts (native charts beside their data).</summary>
    public static ReportFile Write(ReportDefinition report, ReportResult result, IReadOnlyDictionary<string, string?> filters, string format, bool arabic, DateTimeOffset at, IReadOnlyList<ChartData>? charts = null)
    {
        var name = $"{report.Code}-{at:yyyyMMdd-HHmm}";
        return format == ReportFormats.Csv
            ? new ReportFile(name + ".csv", "text/csv; charset=utf-8", Csv(report, result, arabic))
            : new ReportFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(report, result, filters, arabic, at, charts ?? []));
    }

    private static byte[] Csv(ReportDefinition report, ReportResult result, bool arabic)
    {
        var text = new StringBuilder();
        text.AppendJoin(',', result.Columns.Select(c => Cell(Label(report, c.Name, arabic)))).Append("\r\n");
        foreach (var row in result.Rows)
        {
            text.AppendJoin(',', result.Columns.Select(c => Cell(Text(row[c.Name])))).Append("\r\n");
        }

        if (result.Totals is { } totals)
        {
            text.AppendJoin(',', result.Columns.Select((c, i) => Cell(i == 0 && totals[c.Name] is null ? (arabic ? "الإجمالي" : "Total") : Text(totals[c.Name])))).Append("\r\n");
        }

        // A byte order mark so that spreadsheet programs read Arabic correctly.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static byte[] Xlsx(ReportDefinition report, ReportResult result, IReadOnlyDictionary<string, string?> filters, bool arabic, DateTimeOffset at, IReadOnlyList<ChartData> charts)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(arabic ? "التقرير" : "Report");
        sheet.RightToLeft = arabic;
        sheet.Cell(1, 1).Value = arabic ? report.NameAr : report.NameEn;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"{(arabic ? "أُعد في" : "Generated")} {at:yyyy-MM-dd HH:mm}";
        var line = 3;
        foreach (var filter in report.Filters.Where(f => !string.IsNullOrWhiteSpace(filters.GetValueOrDefault(f.Name))))
        {
            sheet.Cell(line, 1).Value = arabic ? filter.LabelAr : filter.LabelEn;
            sheet.Cell(line, 2).Value = filters[filter.Name];
            line++;
        }

        var header = line + 1;
        for (var c = 0; c < result.Columns.Count; c++)
        {
            var cell = sheet.Cell(header, c + 1);
            cell.Value = Label(report, result.Columns[c].Name, arabic);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F7");
        }

        var r = header + 1;
        foreach (var row in result.Rows)
        {
            Write(sheet, r++, result.Columns, row);
        }

        if (result.Totals is { } totals)
        {
            Write(sheet, r, result.Columns, totals);
            if (totals[result.Columns[0].Name] is null)
            {
                sheet.Cell(r, 1).Value = arabic ? "الإجمالي" : "Total";
            }

            sheet.Row(r).Style.Font.Bold = true;
        }

        if (result.Rows.Count > 0)
        {
            sheet.Range(header, 1, header + result.Rows.Count, result.Columns.Count).SetAutoFilter();
        }

        sheet.Columns(1, result.Columns.Count).AdjustToContents(header, Math.Min(header + 500, r));
        var placed = ReportExcelCharts.WriteData(book, charts, arabic);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return ReportExcelCharts.AddCharts(stream.ToArray(), placed, arabic);
    }

    private static void Write(IXLWorksheet sheet, int row, IReadOnlyList<ReportResultColumn> columns, JsonObject values)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var value = values[columns[c].Name];
            var cell = sheet.Cell(row, c + 1);
            if (value is null)
            {
                continue;
            }

            switch (columns[c].Type)
            {
                case DataTypes.Number when value is JsonValue v && v.TryGetValue<decimal>(out var number):
                    cell.Value = number;
                    break;
                case DataTypes.Date when value is JsonValue v && DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date):
                    cell.Value = date;
                    cell.Style.DateFormat.Format = date.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
                    break;
                case DataTypes.Boolean when value is JsonValue v && v.TryGetValue<bool>(out var flag):
                    cell.Value = flag;
                    break;
                default:
                    cell.Value = Text(value);
                    break;
            }
        }
    }

    public static string Label(ReportDefinition report, string column, bool arabic) =>
        report.Columns.FirstOrDefault(c => c.Name == column) is { } def ? (arabic ? def.LabelAr : def.LabelEn) : column;

    private static string Text(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        JsonValue v => v.ToJsonString(),
        _ => value.ToJsonString(),
    };

    private static string Cell(string value)
    {
        // Quoted when needed; a leading =, +, - or @ is prefixed so a spreadsheet does not run it as a formula.
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ? "'" + value : value;
        return safe.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : safe;
    }
}
