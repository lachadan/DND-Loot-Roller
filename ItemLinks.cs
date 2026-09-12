using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DNDLootRoller;

internal sealed record ItemLink(string Url, bool IsSearch);

internal static class ItemLinks
{
    private static readonly Lazy<Dictionary<string, string>> Catalog = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("item_links.json"));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    });

    public static string Key(string text) => Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9+]", "");

    public static bool IsItemUrl(string? text) => Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("www.dndbeyond.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("dndbeyond.com", StringComparison.OrdinalIgnoreCase))
        && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && Regex.IsMatch(uri.AbsolutePath, @"^/(magic-items|equipment)/[a-zA-Z0-9-]+/?$");

    public static ItemLink? For(string? description, string? explicitUrl = null, string? parent = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitUrl) && IsItemUrl(explicitUrl)) return new(explicitUrl, false);
        if (string.IsNullOrWhiteSpace(description) || Regex.IsMatch(description.Trim(), @"^[\d\s,.]+(?:\s*(?:gp|sp|cp|pp|ep|gold|silver|copper)(?:\s+pieces?)?)?$", RegexOptions.IgnoreCase)) return null;
        string query = Regex.Replace(description, @"\s*\(roll\s+d\s*(?:\d+|l2)\)", "", RegexOptions.IgnoreCase).Trim();
        // Figurine subrows name only the animal; keep the parent context.
        if (parent?.StartsWith("Figurine of wondrous power", StringComparison.OrdinalIgnoreCase) == true)
            query = $"Figurine of wondrous power ({query})";
        if (Catalog.Value.TryGetValue(Key(query), out string? url) && IsItemUrl(url)) return new(url, false);
        return new("https://www.dndbeyond.com/magic-items?filter-search=" + Uri.EscapeDataString(query), true);
    }
}
