using suggu.Core.Packs;

namespace suggu.Core.Workspace;

/// <summary>A pack layer resolved to a real project on disk.</summary>
public sealed record LayerProject(string LayerName, string ProjectPath, string Directory, string RootNamespace);

/// <summary>
/// Matches the pack's layer detection patterns (*.Domain, *.Infra ...) against the
/// projects in the solution. This is how generators know where to write.
/// </summary>
public static class LayerResolver
{
    /// <summary>Every pack layer that has a matching project under the solution root.</summary>
    public static IReadOnlyList<LayerProject> ResolveAll(string solutionRoot, PackManifest pack)
    {
        var csprojs = Directory
            .EnumerateFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .ToList();

        var resolved = new List<LayerProject>();
        foreach (var layer in pack.Layers)
        {
            var match = csprojs.FirstOrDefault(p => MatchesLayer(p, layer));
            if (match is not null)
            {
                resolved.Add(ToLayerProject(layer.Name, match));
            }
        }

        return resolved;
    }

    /// <summary>The project for one named pack layer, or null when the solution has none.</summary>
    public static LayerProject? Resolve(string solutionRoot, PackManifest pack, string layerName)
    {
        var layer = pack.FindLayer(layerName);
        return layer is null
            ? null
            : ResolveAll(solutionRoot, pack).FirstOrDefault(l =>
                string.Equals(l.LayerName, layer.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesLayer(string projectPath, LayerSpec layer)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectPath);
        return layer.ProjectPatterns.Any(pattern => MatchesPattern(fileName, pattern));
    }

    // Supports the only wildcard shape packs use: a leading "*." prefix (e.g. "*.Domain").
    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern.StartsWith("*", StringComparison.Ordinal))
        {
            return name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static LayerProject ToLayerProject(string layerName, string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath)!;

        // Convention: root namespace is the project file name (Shop.Domain.csproj -> Shop.Domain).
        var rootNamespace = Path.GetFileNameWithoutExtension(projectPath);

        return new LayerProject(layerName, projectPath, directory, rootNamespace);
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/") || normalized.Contains("/obj/");
    }
}
