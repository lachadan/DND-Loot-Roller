using System.Reflection;
using System.Text.Json;

namespace DNDLootRoller;

internal sealed class LootData
{
    public List<LootTable> Tables { get; set; } = [];
    public List<string> Issues { get; set; } = [];

    public static LootData Load()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("loot_tables.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded loot_tables.json was not found.");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Unable to open embedded loot data.");

        return JsonSerializer.Deserialize<LootData>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Loot data could not be read.");
    }
}

internal sealed class LootTable
{
    public int Die { get; set; }
    public string Name { get; set; } = "";
    public List<LootEntry> Entries { get; set; } = [];
}

internal sealed class LootEntry
{
    public string? BeyondUrl { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public string Description { get; set; } = "";
    public int? SubDie { get; set; }
    public List<SubEntry> SubEntries { get; set; } = [];

    public bool Matches(int roll) => roll >= Min && roll <= Max;
}

internal sealed class SubEntry
{
    public string? BeyondUrl { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public string Description { get; set; } = "";

    public bool Matches(int roll) => roll >= Min && roll <= Max;
}
