using System.Xml.Linq;

namespace suggu.Core.Inspection;

/// <summary>A project and the projects it references (by project name, e.g. "suggu.Core").</summary>
public sealed record ProjectReferences(string Name, string ProjectPath, IReadOnlyList<string> References);

/// <summary>
/// Reads the project-to-project reference graph from the csprojs in a solution.
/// Pure inspection — no console I/O.
/// </summary>
public static class ReferenceInspector
{
    /// <summary>Every project under the solution root with its direct project references, sorted by name.</summary>
    public static IReadOnlyList<ProjectReferences> GetReferenceGraph(string solutionRoot)
    {
        return Directory
            .EnumerateFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .Select(GetReferences)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>One project's direct references, resolved from relative include paths to project names.</summary>
    public static ProjectReferences GetReferences(string projectPath)
    {
        var doc = XDocument.Load(projectPath);

        var references = doc.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectReferences(
            Path.GetFileNameWithoutExtension(projectPath),
            projectPath,
            references);
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/") || normalized.Contains("/obj/");
    }
}
