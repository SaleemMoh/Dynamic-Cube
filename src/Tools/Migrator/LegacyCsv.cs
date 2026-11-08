using System.Text;

namespace DynamicCube.Migrator;

/// <summary>Reads the CSV export of the legacy Apps Control: a header row, then rows; quoted fields may hold commas, quotes and new lines.</summary>
internal static class LegacyCsv
{
    /// <summary>The rows of a file by column name (upper case), or none when the file is missing.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string folder, string table)
    {
        var path = Path.Combine(folder, table + ".csv");
        if (!File.Exists(path))
        {
            return [];
        }

        var records = Parse(File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿'));
        if (records.Count == 0)
        {
            return [];
        }

        var header = records[0].Select(h => h.Trim().ToUpperInvariant()).ToList();
        return [.. records.Skip(1)
            .Where(r => r.Count > 1 || r.Any(v => v.Length > 0))
            .Select(r => (IReadOnlyDictionary<string, string>)header
                .Select((name, i) => (name, value: i < r.Count ? r[i] : string.Empty))
                .GroupBy(c => c.name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().value, StringComparer.Ordinal))];
    }

    private static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
