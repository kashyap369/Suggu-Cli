using suggu.Core.Workspace;

namespace suggu.Core.Generation;

/// <summary>An entity found by the scan: name, subfolder under Entities/, and the namespace it lives in.</summary>
public sealed record EntityRef(string Name, string Parent, string Namespace);

/// <summary>
/// Finds entities by convention (v1 has no Roslyn): every .cs file under
/// Domain/Entities/** is an entity, its subfolder path is preserved so
/// generated interfaces mirror the same structure.
/// </summary>
public static class EntityScanner
{
    public const string DefaultSourceFolder = "Entities";

    public static IReadOnlyList<EntityRef> Scan(LayerProject layer, string sourceFolder = DefaultSourceFolder)
    {
        var root = Path.Combine(layer.Directory, sourceFolder);
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(file => ToEntityRef(layer, sourceFolder, root, file))
            .OrderBy(e => e.Parent, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EntityRef ToEntityRef(LayerProject layer, string sourceFolder, string root, string file)
    {
        // Trim guards against stray spaces in file names (seen in the reference repo: "SystemRole .cs").
        var name = Path.GetFileNameWithoutExtension(file).Trim();

        var parent = (Path.GetDirectoryName(Path.GetRelativePath(root, file)) ?? string.Empty)
            .Replace('\\', '/');

        var suffix = parent.Replace('/', '.');
        var ns = suffix.Length == 0
            ? $"{layer.RootNamespace}.{sourceFolder}"
            : $"{layer.RootNamespace}.{sourceFolder}.{suffix}";

        return new EntityRef(name, parent, ns);
    }
}
