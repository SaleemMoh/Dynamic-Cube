using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Platform.PublicApi;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>
/// A report's charts in its Excel file (ADR-0045): a sheet with each chart's data — the points as the viewer draws them,
/// one column per series — and beside it a native Excel chart over that data (bars up or across, lines, areas, points,
/// a pie, series stacked when asked), so the charts can be edited and redrawn in Excel. Charts Excel cannot draw the way
/// the report does (a line down the Y axis) are drawn as horizontal bars.
/// </summary>
internal static class ReportExcelCharts
{
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";

    /// <summary>The fixed categorical colours of the app's charts; past eight, series are grey.</summary>
    private static readonly string[] Colors = ["2A78D6", "EB6834", "1BAF7A", "EDA100", "E87BA4", "008300", "6250D6", "E34948"];

    private const int ChartWidthColumns = 9;
    private const int ChartHeightRows = 20;

    /// <summary>A chart laid out on the sheet: where its data is and how Excel draws it.</summary>
    internal sealed record Placed(ChartData Chart, Table Table, int Top, int DataColumn, string Kind);

    /// <summary>A chart's data as written: the label column (categories or X values) and one column per series.</summary>
    internal sealed record Table(string LabelHeader, IReadOnlyList<object?> Labels, IReadOnlyList<(string Name, IReadOnlyList<double?> Values)> Series, bool NumericLabels, bool DateLabels);

    /// <summary>Writes the charts' data on a new sheet of the book; returns what <see cref="AddCharts"/> needs to draw them.</summary>
    public static IReadOnlyList<Placed> WriteData(XLWorkbook book, IReadOnlyList<ChartData> charts, bool arabic)
    {
        var drawable = charts.Where(c => c.Rows.Count > 0).ToList();
        if (drawable.Count == 0)
        {
            return [];
        }

        var sheet = book.Worksheets.Add(SheetName(arabic));
        sheet.RightToLeft = arabic;
        var placed = new List<Placed>();
        var top = 1;
        foreach (var chart in drawable)
        {
            var table = TableOf(chart, arabic);
            var title = sheet.Cell(top, 1);
            title.Value = arabic ? chart.TitleAr : chart.TitleEn;
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 12;
            var header = top + 1;
            sheet.Cell(header, 1).Value = table.LabelHeader;
            for (var s = 0; s < table.Series.Count; s++)
            {
                sheet.Cell(header, s + 2).Value = table.Series[s].Name;
            }

            sheet.Range(header, 1, header, table.Series.Count + 1).Style.Font.Bold = true;
            sheet.Range(header, 1, header, table.Series.Count + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F7");
            for (var i = 0; i < table.Labels.Count; i++)
            {
                var cell = sheet.Cell(header + 1 + i, 1);
                switch (table.Labels[i])
                {
                    case DateTime date:
                        cell.Value = date;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                        break;
                    case double number:
                        cell.Value = number;
                        break;
                    case string text:
                        cell.Value = text;
                        break;
                }

                for (var s = 0; s < table.Series.Count; s++)
                {
                    if (table.Series[s].Values[i] is { } value)
                    {
                        sheet.Cell(header + 1 + i, s + 2).Value = value;
                    }
                }
            }

            placed.Add(new Placed(chart, table, top, table.Series.Count + 3, KindOf(chart, table)));
            top += Math.Max(table.Labels.Count + 4, ChartHeightRows + 3);
        }

        sheet.Columns(1, placed.Max(p => p.Table.Series.Count) + 1).AdjustToContents();
        return placed;
    }

    /// <summary>Draws the charts written by <see cref="WriteData"/> into the saved book (native Excel charts).</summary>
    public static byte[] AddCharts(byte[] xlsx, IReadOnlyList<Placed> placed, bool arabic)
    {
        if (placed.Count == 0)
        {
            return xlsx;
        }

        using var stream = new MemoryStream();
        stream.Write(xlsx);
        using (var document = SpreadsheetDocument.Open(stream, isEditable: true))
        {
            var workbook = document.WorkbookPart!;
            var name = SheetName(arabic);
            var sheet = workbook.Workbook.Descendants<Spreadsheet.Sheet>().Single(s => s.Name == name);
            var worksheet = (WorksheetPart)workbook.GetPartById(sheet.Id!);
            var drawings = worksheet.AddNewPart<DrawingsPart>();
            var anchors = new List<XElement>();
            foreach (var (item, index) in placed.Select((p, i) => (p, i)))
            {
                var part = drawings.AddNewPart<ChartPart>();
                using (var chartStream = part.GetStream(FileMode.Create))
                {
                    ChartXml(item, name, arabic).Save(chartStream);
                }

                anchors.Add(Anchor(item, drawings.GetIdOfPart(part), index + 2));
            }

            using (var drawingStream = drawings.GetStream(FileMode.Create))
            {
                new XDocument(new XElement(
                    Xdr + "wsDr",
                    new XAttribute(XNamespace.Xmlns + "xdr", Xdr),
                    new XAttribute(XNamespace.Xmlns + "a", A),
                    new XAttribute(XNamespace.Xmlns + "r", R),
                    new XAttribute(XNamespace.Xmlns + "c", C),
                    anchors)).Save(drawingStream);
            }

            // The <drawing> element goes before the elements the schema puts after it (legacy drawings, tables, extensions).
            var drawing = new Spreadsheet.Drawing { Id = worksheet.GetIdOfPart(drawings) };
            var root = worksheet.Worksheet;
            var after = root.ChildElements.FirstOrDefault(e => e is Spreadsheet.LegacyDrawing or Spreadsheet.LegacyDrawingHeaderFooter
                or Spreadsheet.Picture or Spreadsheet.OleObjects or Spreadsheet.Controls or Spreadsheet.WebPublishItems or Spreadsheet.TableParts
                or Spreadsheet.WorksheetExtensionList);
            if (after is null)
            {
                root.Append(drawing);
            }
            else
            {
                root.InsertBefore(drawing, after);
            }

            root.Save();
        }

        return stream.ToArray();
    }

    private static string SheetName(bool arabic) => arabic ? "الرسوم" : "Charts";

    // ------------------------------------------------------------------ the data

    /// <summary>The points of a chart as the viewer draws them: grouped (categories × series) or the rows (X × a column per series).</summary>
    private static Table TableOf(ChartData chart, bool arabic)
    {
        string Label(ChartFieldData f) => arabic ? f.LabelAr : f.LabelEn;
        if (chart.Category == "ROWS")
        {
            var xf = chart.X[0];
            var yf = chart.Y[0];
            var names = chart.Series is null ? [Label(yf)] : Distinct(chart.Rows.Select(r => FieldText(r["s"], chart.Series, arabic)));
            var dateLabels = xf.Type == DataTypes.Date;
            var numericLabels = xf.Type == DataTypes.Number;
            var labels = chart.Rows.Select(r => dateLabels ? (object?)AsDate(r[xf.Name]) : numericLabels ? Number(r[xf.Name]) : FieldText(r[xf.Name], xf, arabic)).ToList();
            var series = names.Select(n => (n, (IReadOnlyList<double?>)[.. chart.Rows.Select(r =>
                chart.Series is null || FieldText(r["s"], chart.Series, arabic) == n ? Number(r[yf.Name]) : null)])).ToList();
            return new Table(Label(xf), labels, series, numericLabels || dateLabels, dateLabels);
        }

        var category = chart.Category == "Y" ? chart.Y[0] : chart.X[0];
        var values = chart.Category == "Y" ? chart.X : chart.Y;
        var categories = Distinct(chart.Rows.Select(r => FieldText(r[category.Name], category, arabic)));
        var index = categories.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i, StringComparer.Ordinal);
        List<(string, IReadOnlyList<double?>)> columns = [];
        if (chart.Series is { } splitBy)
        {
            foreach (var name in Distinct(chart.Rows.Select(r => FieldText(r["s"], splitBy, arabic))))
            {
                var column = new double?[categories.Count];
                foreach (var row in chart.Rows.Where(r => FieldText(r["s"], splitBy, arabic) == name))
                {
                    column[index[FieldText(row[category.Name], category, arabic)]] = Number(row[values[0].Name]);
                }

                columns.Add((name, column));
            }
        }
        else
        {
            foreach (var value in values)
            {
                var column = new double?[categories.Count];
                foreach (var row in chart.Rows)
                {
                    column[index[FieldText(row[category.Name], category, arabic)]] = Number(row[value.Name]);
                }

                columns.Add((Label(value), column));
            }
        }

        return new Table(Label(category), [.. categories], columns, false, false);
    }

    private static List<string> Distinct(IEnumerable<string> values) => [.. values.Distinct(StringComparer.Ordinal)];

    private static double? Number(JsonNode? value) =>
        value is JsonValue v && double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static DateTime? AsDate(JsonNode? value) =>
        value is JsonValue v && DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

    /// <summary>A value as text: a date by its period (2026-03, 2026 Q1, 2026), yes/no, numbers and text as they are.</summary>
    internal static string FieldText(JsonNode? value, ChartFieldData field, bool arabic)
    {
        if (value is null)
        {
            return "—";
        }

        var text = value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value.ToJsonString();
        if (field.Type == DataTypes.Date && AsDate(value) is { } date)
        {
            return field.Bucket switch
            {
                "YEAR" => date.ToString("yyyy", CultureInfo.InvariantCulture),
                "QUARTER" => $"{date:yyyy} Q{((date.Month - 1) / 3) + 1}",
                "MONTH" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                _ => date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            };
        }

        if (field.Type == DataTypes.Boolean && value is JsonValue b && b.TryGetValue<bool>(out var flag))
        {
            return flag ? (arabic ? "نعم" : "Yes") : (arabic ? "لا" : "No");
        }

        return text;
    }

    // ------------------------------------------------------------------ the chart

    /// <summary>How Excel draws the chart: bar (up), barH (across), line, area, markers (points on categories), scatter, scatterLine, pie.</summary>
    private static string KindOf(ChartData chart, Table table)
    {
        if (chart.Type == ReportChartTypes.Pie)
        {
            return "pie";
        }

        if (chart.Category == "ROWS")
        {
            if (table.NumericLabels)
            {
                return chart.Type is ReportChartTypes.Line or ReportChartTypes.Area ? "scatterLine" : "scatter";
            }

            return chart.Type switch { ReportChartTypes.Bar => "bar", ReportChartTypes.Line => "line", ReportChartTypes.Area => "area", _ => "markers" };
        }

        // Grouped down the Y axis: Excel draws categories down the side only as horizontal bars.
        if (chart.Category == "Y")
        {
            return "barH";
        }

        return chart.Type switch { ReportChartTypes.Line => "line", ReportChartTypes.Area => "area", ReportChartTypes.Scatter => "markers", _ => "bar" };
    }

    private static string Ref(string sheet, int column, int fromRow, int toRow) =>
        $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'!${ColumnName(column)}${fromRow}:${ColumnName(column)}${toRow}";

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        for (var n = column; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + ((n - 1) % 26)) + name;
        }

        return name;
    }

    private static XElement Val(string name, object value) => new(C + name, new XAttribute("val", value));

    private static XElement Rich(XName name, string text) => new(
        name,
        new XElement(C + "tx", new XElement(C + "rich", new XElement(A + "bodyPr"), new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", text))))),
        Val("overlay", 0));

    private static XElement Fill(string color) => new(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", color)));

    private static XElement StrRef(string formula, List<string> values) => new(
        C + "strRef",
        new XElement(C + "f", formula),
        new XElement(C + "strCache", Val("ptCount", values.Count), values.Select((v, i) => new XElement(C + "pt", new XAttribute("idx", i), new XElement(C + "v", v)))));

    private static XElement NumRef(string formula, IReadOnlyList<double?> values, string format = "General") => new(
        C + "numRef",
        new XElement(C + "f", formula),
        new XElement(
            C + "numCache",
            new XElement(C + "formatCode", format),
            Val("ptCount", values.Count),
            values.Select((v, i) => (v, i)).Where(p => p.v is not null).Select(p => new XElement(C + "pt", new XAttribute("idx", p.i), new XElement(C + "v", p.v!.Value.ToString(CultureInfo.InvariantCulture))))));

    private static XDocument ChartXml(Placed item, string sheet, bool arabic)
    {
        var table = item.Table;
        var header = item.Top + 1;
        var first = header + 1;
        var last = header + table.Labels.Count;
        var labelTexts = table.Labels.Select(l => l switch
        {
            DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            double n => n.ToString(CultureInfo.InvariantCulture),
            _ => l?.ToString() ?? string.Empty,
        }).ToList();
        var labelRef = Ref(sheet, 1, first, last);
        var kind = item.Kind;
        var stacked = item.Chart.Stacked && kind is "bar" or "barH" or "area";
        var line = kind is "line" or "scatterLine";

        XElement Series(int s)
        {
            var (name, values) = table.Series[s];
            var color = s < Colors.Length ? Colors[s] : "9A9A94";
            var ser = new XElement(
                C + "ser",
                Val("idx", s),
                Val("order", s),
                new XElement(C + "tx", StrRef(Ref(sheet, s + 2, header, header), [name])));
            if (kind == "pie")
            {
                ser.Add(table.Labels.Select((_, i) => new XElement(
                    C + "dPt", Val("idx", i), Val("bubble3D", 0), new XElement(C + "spPr", Fill(i < Colors.Length ? Colors[i] : "9A9A94")))));
            }
            else if (kind is "bar" or "barH" or "area")
            {
                ser.Add(new XElement(C + "spPr", Fill(color)));
                if (kind != "area")
                {
                    ser.Add(Val("invertIfNegative", 0));
                }
            }
            else
            {
                ser.Add(new XElement(C + "spPr", line
                    ? new XElement(A + "ln", new XAttribute("w", 28575), Fill(color))
                    : new XElement(A + "ln", new XElement(A + "noFill"))));
                ser.Add(new XElement(C + "marker", Val("symbol", "circle"), Val("size", 6), new XElement(C + "spPr", Fill(color))));
            }

            var valueRef = Ref(sheet, s + 2, first, last);
            if (kind is "scatter" or "scatterLine")
            {
                ser.Add(new XElement(C + "xVal", NumRef(labelRef, [.. table.Labels.Select(l => l switch { DateTime d => d.ToOADate(), double n => n, _ => (double?)null })], table.DateLabels ? "yyyy-mm-dd" : "General")));
                ser.Add(new XElement(C + "yVal", NumRef(valueRef, values)));
                ser.Add(Val("smooth", 0));
            }
            else
            {
                ser.Add(new XElement(C + "cat", StrRef(labelRef, labelTexts)));
                ser.Add(new XElement(C + "val", NumRef(valueRef, values)));
                if (kind is "line" or "markers")
                {
                    ser.Add(Val("smooth", 0));
                }
            }

            return ser;
        }

        var series = Enumerable.Range(0, kind == "pie" ? 1 : table.Series.Count).Select(Series).ToList();
        const int categoryAxis = 500001;
        const int valueAxis = 500002;
        XElement plot = kind switch
        {
            "pie" => new XElement(C + "pieChart", Val("varyColors", 1), series, Val("firstSliceAng", 0)),
            "bar" or "barH" => new XElement(
                C + "barChart",
                Val("barDir", kind == "barH" ? "bar" : "col"),
                Val("grouping", stacked ? "stacked" : "clustered"),
                Val("varyColors", 0),
                series,
                Val("gapWidth", 80),
                stacked ? Val("overlap", 100) : null,
                Val("axId", categoryAxis),
                Val("axId", valueAxis)),
            "area" => new XElement(C + "areaChart", Val("grouping", stacked ? "stacked" : "standard"), Val("varyColors", 0), series, Val("axId", categoryAxis), Val("axId", valueAxis)),
            "scatter" or "scatterLine" => new XElement(C + "scatterChart", Val("scatterStyle", "lineMarker"), Val("varyColors", 0), series, Val("axId", categoryAxis), Val("axId", valueAxis)),
            _ => new XElement(C + "lineChart", Val("grouping", "standard"), Val("varyColors", 0), series, Val("marker", 1), Val("axId", categoryAxis), Val("axId", valueAxis)),
        };

        string Label(IReadOnlyList<ChartFieldData> fields) => string.Join(" · ", fields.Select(f => arabic ? f.LabelAr : f.LabelEn));
        var xTitle = Label(item.Chart.X);
        var yTitle = Label(item.Chart.Y);
        XElement Axis(string type, int id, int cross, string position, string? title, bool gridlines, string? format) => new(
            C + type,
            Val("axId", id),
            new XElement(C + "scaling", Val("orientation", "minMax")),
            Val("delete", 0),
            Val("axPos", position),
            gridlines ? new XElement(C + "majorGridlines", new XElement(C + "spPr", new XElement(A + "ln", Fill("E5E7EB")))) : null,
            title is null ? null : Rich(C + "title", title),
            format is null ? null : new XElement(C + "numFmt", new XAttribute("formatCode", format), new XAttribute("sourceLinked", 0)),
            Val("majorTickMark", "out"),
            Val("minorTickMark", "none"),
            Val("tickLblPos", "nextTo"),
            Val("crossAx", cross),
            Val("crosses", "autoZero"),
            type == "catAx" ? new object[] { Val("auto", 1), Val("lblAlgn", "ctr"), Val("lblOffset", 100), Val("noMultiLvlLbl", 0) } : new object[] { Val("crossBetween", "between") });

        var plotArea = new XElement(C + "plotArea", new XElement(C + "layout"), plot);
        switch (kind)
        {
            case "pie":
                break;
            case "barH":
                // Categories down the side (the report's Y axis), values across (its X axis).
                plotArea.Add(Axis("catAx", categoryAxis, valueAxis, "l", yTitle, false, null), Axis("valAx", valueAxis, categoryAxis, "b", xTitle, true, null));
                break;
            case "scatter" or "scatterLine":
                plotArea.Add(
                    Axis("valAx", categoryAxis, valueAxis, "b", xTitle, false, table.DateLabels ? "yyyy-mm-dd" : null),
                    Axis("valAx", valueAxis, categoryAxis, "l", yTitle, true, null));
                break;
            default:
                plotArea.Add(Axis("catAx", categoryAxis, valueAxis, "b", xTitle, false, null), Axis("valAx", valueAxis, categoryAxis, "l", yTitle, true, null));
                break;
        }

        var showLegend = kind == "pie" || table.Series.Count > 1;
        return new XDocument(new XElement(
            C + "chartSpace",
            new XAttribute(XNamespace.Xmlns + "c", C),
            new XAttribute(XNamespace.Xmlns + "a", A),
            new XAttribute(XNamespace.Xmlns + "r", R),
            Val("roundedCorners", 0),
            new XElement(
                C + "chart",
                Rich(C + "title", arabic ? item.Chart.TitleAr : item.Chart.TitleEn),
                Val("autoTitleDeleted", 0),
                plotArea,
                showLegend ? new XElement(C + "legend", Val("legendPos", "b"), Val("overlay", 0)) : null,
                Val("plotVisOnly", 1),
                Val("dispBlanksAs", "gap"))));
    }

    /// <summary>Where a chart sits: beside its data, nine columns wide and twenty rows high.</summary>
    private static XElement Anchor(Placed item, string chartId, int id)
    {
        XElement Point(string name, int column, int row) => new(
            Xdr + name,
            new XElement(Xdr + "col", column),
            new XElement(Xdr + "colOff", 0),
            new XElement(Xdr + "row", row),
            new XElement(Xdr + "rowOff", 0));
        var fromColumn = item.DataColumn - 1;
        var fromRow = item.Top - 1;
        return new XElement(
            Xdr + "twoCellAnchor",
            new XAttribute("editAs", "oneCell"),
            Point("from", fromColumn, fromRow),
            Point("to", fromColumn + ChartWidthColumns, fromRow + ChartHeightRows),
            new XElement(
                Xdr + "graphicFrame",
                new XAttribute("macro", string.Empty),
                new XElement(Xdr + "nvGraphicFramePr", new XElement(Xdr + "cNvPr", new XAttribute("id", id), new XAttribute("name", $"Chart {id - 1}")), new XElement(Xdr + "cNvGraphicFramePr")),
                new XElement(Xdr + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0))),
                new XElement(A + "graphic", new XElement(
                    A + "graphicData",
                    new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
                    new XElement(C + "chart", new XAttribute(XNamespace.Xmlns + "r", R), new XAttribute(R + "id", chartId))))),
            new XElement(Xdr + "clientData"));
    }
}
