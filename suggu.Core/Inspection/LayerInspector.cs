using System.Xml.Linq;

namespace suggu.Core.Inspection;

/// <summary>A NuGet package reference: name + version.</summary>
public sealed record PackageRef(string Name, string Version);

/// <summary>A project in the solution, treated as a "layer" (e.g. Cli, Core, Tests).</summary>
public sealed record ProjectLayer(string Name, string ProjectPath);

/// <summary>
/// Reads projects ("layers") and their packages from a solution on disk.
/// Pure inspection — no console I/O — so a future MCP server can reuse it unchanged.
/// </summary>
public static class LayerInspector
{
    /// <summary>Every project under the solution root, as a layer.</summary>
    public static IReadOnlyList<ProjectLayer> GetLayers(string solutionRoot)
    {
        return Directory
            .EnumerateFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .Select(p => new ProjectLayer(LayerNameOf(p), p))
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The project that owns <paramref name="startDirectory"/> — the nearest .csproj walking up.</summary>
    public static ProjectLayer? GetCurrentLayer(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var csproj = dir.EnumerateFiles("*.csproj").FirstOrDefault();
            if (csproj is not null)
            {
                return new ProjectLayer(LayerNameOf(csproj.FullName), csproj.FullName);
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Find a layer by name, case-insensitive (e.g. "cli" matches suggu.Cli).</summary>
    public static ProjectLayer? FindLayer(string solutionRoot, string layerName)
    {
        return GetLayers(solutionRoot).FirstOrDefault(l =>
            string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(l.ProjectPath)
                .EndsWith("." + layerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Read the &lt;PackageReference&gt; entries (name + version) from a .csproj.</summary>
    public static IReadOnlyList<PackageRef> GetPackages(string projectPath)
    {
        var doc = XDocument.Load(projectPath);

        return doc.Descendants("PackageReference")
            .Select(e => new PackageRef(
                (string?)e.Attribute("Include") ?? "(unknown)",
                (string?)e.Attribute("Version") ?? e.Element("Version")?.Value ?? "(no version)"))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // "suggu.Cli" -> "Cli"; a name with no dot is returned as-is.
    private static string LayerNameOf(string projectPath)
    {
        var name = Path.GetFileNameWithoutExtension(projectPath);
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 && lastDot < name.Length - 1 ? name[(lastDot + 1)..] : name;
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/") || normalized.Contains("/obj/");
    }
}
