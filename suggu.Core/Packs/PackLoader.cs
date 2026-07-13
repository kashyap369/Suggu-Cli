using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace suggu.Core.Packs;

/// <summary>
/// Where a pack's files come from. One read path for both embedded and on-disk packs,
/// so external user packs are free later.
/// </summary>
public interface IPackFileProvider
{
    /// <summary>Read a pack-relative text file (e.g. "pack.json", "templates/entity.txt").</summary>
    string ReadText(string relativePath);

    bool Exists(string relativePath);
}

/// <summary>Loads a pack manifest (and later, its templates) through one code path.</summary>
public static class PackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public const string SupportedSchemaVersion = "1";

    /// <summary>The default pack compiled into suggu.Core.</summary>
    public static (PackManifest Manifest, IPackFileProvider Files) LoadDefault()
    {
        var files = new EmbeddedPackFileProvider(typeof(PackLoader).Assembly, "suggu.Core.Packs.Default");
        return (LoadManifest(files), files);
    }

    /// <summary>An external pack on disk — same code path as the embedded default.</summary>
    public static (PackManifest Manifest, IPackFileProvider Files) LoadFromDirectory(string directory)
    {
        var files = new DirectoryPackFileProvider(directory);
        return (LoadManifest(files), files);
    }

    private static PackManifest LoadManifest(IPackFileProvider files)
    {
        var json = files.ReadText("pack.json");
        var manifest = JsonSerializer.Deserialize<PackManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("pack.json deserialized to null");

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"pack '{manifest.Name}' has schema version '{manifest.SchemaVersion}' but this suggu supports '{SupportedSchemaVersion}'. " +
                "Update the pack (or suggu) so they match.");
        }

        return manifest;
    }
}

/// <summary>Reads pack files from embedded resources under a root namespace prefix.</summary>
internal sealed class EmbeddedPackFileProvider(Assembly assembly, string resourcePrefix) : IPackFileProvider
{
    public string ReadText(string relativePath)
    {
        using var stream = assembly.GetManifestResourceStream(ResourceName(relativePath))
            ?? throw new FileNotFoundException($"embedded pack file not found: {relativePath}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public bool Exists(string relativePath) =>
        assembly.GetManifestResourceStream(ResourceName(relativePath)) is not null;

    // "templates/entity.txt" -> "suggu.Core.Packs.Default.templates.entity.txt"
    private string ResourceName(string relativePath) =>
        $"{resourcePrefix}.{relativePath.Replace('/', '.').Replace('\\', '.')}";
}

/// <summary>Reads pack files from a directory on disk.</summary>
internal sealed class DirectoryPackFileProvider(string root) : IPackFileProvider
{
    public string ReadText(string relativePath) => File.ReadAllText(Path.Combine(root, relativePath));

    public bool Exists(string relativePath) => File.Exists(Path.Combine(root, relativePath));
}
