using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DNDLootRoller;

// Reads values directly from XLSX: no Excel installation or third-party package required.
internal static class SpreadsheetImporter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static LootData Read(string path)
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose an .xlsx workbook. Save older .xls files as .xlsx first.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 32 * 1024 * 1024)
            throw new InvalidDataException("Workbook is too large. Use a loot-only workbook under 32 MB uncompressed.");
        XDocument ReadXml(string name)
        {
            using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException($"Missing workbook part: {name}")).Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? new List<string>() :
            ReadXml("xl/sharedStrings.xml").Descendants(S + "si").Select(x => string.Concat(x.Descendants(S + "t").Select(t => t.Value))).ToList();
        var sheets = ReadXml("xl/workbook.xml").Descendants(S + "sheet").ToList();
        if (sheets.Count != 6) throw new InvalidDataException("The workbook must contain exactly six worksheet tabs, in D6 order (first tab = 1, last = 6).");
        var rels = ReadXml("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(x => (string)x.Attribute("Id")!);
        var data = new LootData();
        for (int i = 0; i < 6; i++)
        {
            var sheet = sheets[i];
            string name = (string?)sheet.Attribute("name") ?? $"Sheet {i + 1}";
            string id = (string?)sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships")) ?? "";
            if (!rels.TryGetValue(id, out var rel) || !((string?)rel.Attribute("Type") ?? "").EndsWith("/worksheet") || (string?)rel.Attribute("TargetMode") == "External")
                throw new InvalidDataException($"'{name}' must be a worksheet.");
            string target = (string?)rel.Attribute("Target") ?? "";
            string part = new Uri(new Uri("https://workbook/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            var table = new LootTable { Die = i + 1, Name = name };
            data.Tables.Add(table);
            data.Issues.AddRange((string?)sheet.Attribute("state") is "hidden" or "veryHidden" ? new[] { $"Table {i + 1}: '{name}' is hidden but included in D6 order." } : Array.Empty<string>());
            LootEntry? parent = null;
            foreach (var row in ReadXml(part).Descendants(S + "sheetData").Elements(S + "row"))
            {
                string location = $"'{name}', row {(string?)row.Attribute("r")}";
                var cells = row.Elements(S + "c").ToDictionary(c => Regex.Replace((string?)c.Attribute("r") ?? "", "[0-9]", ""));
                string Value(string col)
                {
                    if (!cells.TryGetValue(col, out var cell)) return "";
                    if (cell.Element(S + "f") != null) throw new InvalidDataException($"{location}, column {col}: formulas are not supported. Paste as values first.");
                    string type = (string?)cell.Attribute("t") ?? "";
                    string v = cell.Element(S + "v")?.Value ?? "";
                    if (type == "e") throw new InvalidDataException($"{location}: Excel error value.");
                    if (type == "s") return int.TryParse(v, out int index) && index >= 0 && index < strings.Count ? strings[index].Trim() : throw new InvalidDataException($"{location}: invalid shared string.");
                    if (type == "inlineStr") return string.Concat(cell.Descendants(S + "t").Select(t => t.Value)).Trim();
                    return v.Trim();
                }
                string a = Value("A"), b = Value("B"), url = Value("C");
                if (a == "" && b == "" && url == "") continue;
                if (table.Entries.Count == 0 && Regex.IsMatch(a, "^(roll|d100|range)$", RegexOptions.IgnoreCase) && Regex.IsMatch(b, "^(loot|description|item)$", RegexOptions.IgnoreCase)) continue;
                if (url.Length > 0 && !ItemLinks.IsItemUrl(url)) throw new InvalidDataException($"{location}, column C: enter an HTTPS D&D Beyond item-page URL or leave it blank.");
                if (b.Length == 0 || b.Length > 4000) throw new InvalidDataException($"{location}: enter a loot description of 1–4000 characters in column B.");
                if (a is "-" or "–" or "—")
                {
                    if (parent?.SubDie == null) throw new InvalidDataException($"{location}: subtable row needs a preceding loot entry containing '(roll d8)' or '(roll d12)'.");
                    int colon = b.IndexOf(':');
                    if (colon < 0 || string.IsNullOrWhiteSpace(b[(colon + 1)..])) throw new InvalidDataException($"{location}: use '1-2: Loot description' in column B.");
                    var (min, max) = ParseRange(b[..colon], parent.SubDie.Value, location);
                    parent.SubEntries.Add(new SubEntry { Min = min, Max = max, Description = b[(colon + 1)..].Trim(), BeyondUrl = url.Length == 0 ? null : url });
                }
                else
                {
                    var (min, max) = ParseRange(a, 100, location);
                    parent = new LootEntry { Min = min, Max = max, Description = b, BeyondUrl = url.Length == 0 ? null : url };
                    var die = Regex.Match(b, @"\broll\s+d\s*([0-9]+|l2)\b", RegexOptions.IgnoreCase);
                    if (die.Success)
                    {
                        string n = die.Groups[1].Value.ToLowerInvariant().Replace("l", "1");
                        if (!int.TryParse(n, out int sides) || sides < 2 || sides > 100) throw new InvalidDataException($"{location}: subtable die must be D2–D100.");
                        parent.SubDie = sides;
                    }
                    table.Entries.Add(parent);
                }
            }
            if (table.Entries.Count == 0) throw new InvalidDataException($"'{name}' contains no loot entries in columns A and B.");
        }
        Validate(data);
        return data;
    }

    private static (int Min, int Max) ParseRange(string text, int sides, string location)
    {
        var match = Regex.Match(text.Trim(), @"^(\d{1,3})(?:\s*[-–—]\s*(\d{1,3}))?$");
        if (!match.Success) throw new InvalidDataException($"{location}: invalid roll '{text}'. Use a number or range such as 1-15; format ranges as Text in Excel.");
        int min = int.Parse(match.Groups[1].Value), max = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : min;
        if (min < 1 || max > sides || min > max) throw new InvalidDataException($"{location}: roll range must be within 1–{sides}, lowest number first.");
        return (min, max);
    }

    public static void Validate(LootData data)
    {
        if (data.Tables == null || data.Tables.Count != 6 || !data.Tables.Select(t => t.Die).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 6)))
            throw new InvalidDataException("Saved loot must contain tables 1–6 exactly once.");
        foreach (var t in data.Tables)
        {
            if (t.Entries == null || t.Entries.Count == 0) throw new InvalidDataException($"Table {t.Die} is empty.");
            foreach (var e in t.Entries)
            {
                if ((!string.IsNullOrEmpty(e.BeyondUrl) && !ItemLinks.IsItemUrl(e.BeyondUrl)) || (e.SubEntries?.Any(s => !string.IsNullOrEmpty(s.BeyondUrl) && !ItemLinks.IsItemUrl(s.BeyondUrl)) ?? false))
                    throw new InvalidDataException($"Table {t.Die}: invalid D&D Beyond item URL.");
                if (e.Min < 1 || e.Max > 100 || e.Min > e.Max || string.IsNullOrWhiteSpace(e.Description) || e.SubEntries == null)
                    throw new InvalidDataException($"Table {t.Die} has an invalid entry.");
                if (e.SubDie != null || e.SubEntries.Count > 0)
                {
                    if (e.SubDie is null or < 2 or > 100 || e.SubEntries.Count == 0 || e.SubEntries.Any(s => s.Min < 1 || s.Max > e.SubDie || s.Min > s.Max || string.IsNullOrWhiteSpace(s.Description)))
                        throw new InvalidDataException($"Table {t.Die}, roll {e.Min}: invalid or missing subtable rows.");
                    Coverage(e.SubDie.Value, r => e.SubEntries.Count(s => s.Matches(r)), $"Table {t.Die}, roll {e.Min}, D{e.SubDie}", data.Issues);
                }
            }
            Coverage(100, r => t.Entries.Count(e => e.Matches(r)), $"Table {t.Die}", data.Issues);
        }
    }

    private static void Coverage(int sides, Func<int, int> count, string label, List<string> issues)
    {
        var gaps = Enumerable.Range(1, sides).Where(r => count(r) == 0).ToArray();
        var overlaps = Enumerable.Range(1, sides).Where(r => count(r) > 1).ToArray();
        if (gaps.Length > 0) issues.Add($"{label}: no loot for rolls {string.Join(", ", gaps)}.");
        if (overlaps.Length > 0) issues.Add($"{label}: overlapping rolls {string.Join(", ", overlaps)}. First matching row wins.");
    }
}
