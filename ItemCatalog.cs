using System.Text.Json;
using System.Text.Json.Serialization;

namespace DNDLootRoller;

/// <summary>Campaign-independent item catalog. A source must be reviewed before its entries are enabled.</summary>
public sealed record CatalogItem(
    string Id, string Name, string Category, string Rarity, string SourceId,
    string Ruleset, string? Description = null, string? Url = null,
    int? MinimumPartyLevel = null, string? License = null);

public sealed record CatalogSource(
    string Id, string Name, string Kind, bool Enabled = true,
    bool Reviewed = false, string? License = null, string? Homepage = null);

public sealed class ItemCatalog
{
    public int SchemaVersion { get; set; } = 1;
    public List<CatalogSource> Sources { get; set; } = DefaultSources();
    public List<CatalogItem> Items { get; set; } = new();

    public static List<CatalogSource> DefaultSources() => new()
    {
        new("srd", "D&D 5e SRD", "srd", true, false),
        new("open5e", "Open5e", "open", true, false),
        new("third-party", "Third-party libraries", "third", true, false),
        new("custom", "Imported and homebrew", "custom", true, false)
    };

    public IReadOnlyList<CatalogItem> EligibleItems(int partyLevel, IEnumerable<string>? categories = null)
    {
        if (partyLevel is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(partyLevel));
        var enabled = Sources.Where(s => s.Enabled && s.Reviewed)
            .Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categorySet = categories?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Items.Where(i => enabled.Contains(i.SourceId)
            && (!i.MinimumPartyLevel.HasValue || i.MinimumPartyLevel.Value <= partyLevel)
            && (categorySet is null || categorySet.Contains(i.Category)))
            .ToList();
    }

    public void Import(string sourceId, IEnumerable<CatalogItem> items)
    {
        if (!Sources.Any(s => string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Unknown catalog source.", nameof(sourceId));
        var incoming = items.ToList();
        if (incoming.Any(i => string.IsNullOrWhiteSpace(i.Id)
            || string.IsNullOrWhiteSpace(i.Name)
            || string.IsNullOrWhiteSpace(i.Category)
            || string.IsNullOrWhiteSpace(i.Rarity)
            || !string.Equals(i.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Invalid item or mismatched source ID.");
        var duplicates = incoming.GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);
        if (duplicates) throw new InvalidDataException("Duplicate item IDs in import.");
        Items.RemoveAll(i => string.Equals(i.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));
        Items.AddRange(incoming);
    }
}

public static class ItemCatalogStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DNDLootRoller", "item-catalog.json");

    public static ItemCatalog Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new ItemCatalog();
        var result = JsonSerializer.Deserialize<ItemCatalog>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("Item catalog is empty.");
        if (result.SchemaVersion != 1 || result.Sources is null || result.Items is null)
            throw new InvalidDataException("Unsupported or invalid item catalog schema.");
        return result;
    }

    public static void Save(ItemCatalog catalog, string? path = null)
    {
        path ??= DefaultPath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(catalog, Options));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else
                File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
