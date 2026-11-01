using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace DNDLootRoller;

internal sealed class HistoryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Player { get; set; } = "Unassigned";
    public string Workbook { get; set; } = "";
    public string Sheet { get; set; } = "";
    public int D6 { get; set; }
    public int D100 { get; set; }
    public int? SubDie { get; set; }
    public int? SubRoll { get; set; }
    public string Loot { get; set; } = "";
}

internal static class HistoryStore
{
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNDLootRoller", "loot-history.json");
    public static List<HistoryRecord> Read(string? path = null)
    {
        path ??= FilePath;
        if (!File.Exists(path)) return [];
        return JsonSerializer.Deserialize<List<HistoryRecord>>(File.ReadAllText(path)) ?? throw new InvalidDataException("History file is empty or invalid.");
    }
    public static void Change(Action<List<HistoryRecord>> change, string? path = null)
    {
        using var mutex = new Mutex(false, @"Local\DNDLootRollerHistory31");
        bool locked = false;
        try
        {
            try { locked = mutex.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { locked = true; }
            if (!locked) throw new IOException("History is busy. Please try again.");
            path ??= FilePath;
            var rows = Read(path); // Never replace unreadable history.
            change(rows);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { if (locked) mutex.ReleaseMutex(); }
    }
    public static List<HistoryRecord> Filter(IEnumerable<HistoryRecord> rows, string player, string workbook, DateTime? from, DateTime? to) =>
        rows.Where(r => (player.Length == 0 || r.Player.Equals(player, StringComparison.OrdinalIgnoreCase))
            && (workbook.Length == 0 || r.Workbook.Equals(workbook, StringComparison.OrdinalIgnoreCase))
            && (!from.HasValue || r.Timestamp.LocalDateTime.Date >= from.Value.Date)
            && (!to.HasValue || r.Timestamp.LocalDateTime.Date <= to.Value.Date))
            .OrderByDescending(r => r.Timestamp).ToList();

    public static void Export(string path, IEnumerable<HistoryRecord> records)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var data = new XElement(ns + "sheetData");
        void Row(IEnumerable<string> values) => data.Add(new XElement(ns + "row", values.Select(v => new XElement(ns + "c", new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), v))))));
        Row(["Record ID", "Timestamp (with offset)", "Player", "Workbook", "Sheet", "D6", "D100", "Sub die", "Sub roll", "Loot"]);
        foreach (var r in records) Row([r.Id.ToString(), r.Timestamp.ToString("O"), r.Player, r.Workbook, r.Sheet, r.D6.ToString(), r.D100.ToString(), r.SubDie?.ToString() ?? "", r.SubRoll?.ToString() ?? "", r.Loot]);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                void Part(string name, string xml) { using var w = new StreamWriter(zip.CreateEntry(name).Open()); w.Write(xml); }
                Part("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Part("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Part("xl/workbook.xml", new XElement(ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", rel), new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "Loot History"), new XAttribute("sheetId", 1), new XAttribute(rel + "id", "rId1")))).ToString());
                Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                Part("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data).ToString());
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
