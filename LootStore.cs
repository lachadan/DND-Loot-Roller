using System.Text.Json;

namespace DNDLootRoller;

internal sealed class SavedLoot
{
    public string Name { get; set; } = "Imported workbook";
    public LootData Data { get; set; } = new();
}

internal static class LootStore
{
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNDLootRoller", "active-loot.json");

    public static SavedLoot? Read(string? path = null)
    {
        path ??= FilePath;
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Saved loot file is too large.");
        var saved = JsonSerializer.Deserialize<SavedLoot>(File.ReadAllText(path)) ?? throw new InvalidDataException("Saved loot is empty.");
        if (saved.Data == null) throw new InvalidDataException("Saved loot is missing.");
        saved.Data.Issues = new();
        SpreadsheetImporter.Validate(saved.Data);
        return saved;
    }

    public static void Save(SavedLoot saved, string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(saved));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Reset() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
