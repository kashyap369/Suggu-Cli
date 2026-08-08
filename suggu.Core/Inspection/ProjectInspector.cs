using System.Xml.Linq;

namespace suggu.Core.Inspection;

public sealed record PackageRef(string Name, string Version);

public sealed record DotnetProject(string Name, string ProjectPath);

/// <summary>Reads .NET projects and their NuGet package references without architecture assumptions.</summary>
public static class ProjectInspector
{
    public static IReadOnlyList<DotnetProject> GetProjects(string solutionRoot) =>
        Directory
            .EnumerateFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => new DotnetProject(ProjectNameOf(path), path))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static DotnetProject? GetCurrentProject(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var project = directory.EnumerateFiles("*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return new DotnetProject(ProjectNameOf(project.FullName), project.FullName);
            }
            directory = directory.Parent;
        }
        return null;
    }

    public static DotnetProject? FindProject(string solutionRoot, string projectName) =>
        GetProjects(solutionRoot).FirstOrDefault(project =>
            string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(project.ProjectPath)
                .Equals(projectName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(project.ProjectPath)
                .EndsWith("." + projectName, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<PackageRef> GetPackages(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var centralVersions = ReadCentralPackageVersions(projectPath);
        return document.Descendants("PackageReference")
            .Select(element =>
            {
                var name = (string?)element.Attribute("Include") ?? "(unknown)";
                var version = (string?)element.Attribute("VersionOverride")
                    ?? (string?)element.Attribute("Version")
                    ?? element.Elements().FirstOrDefault(child => child.Name.LocalName is "VersionOverride" or "Version")?.Value
                    ?? centralVersions.GetValueOrDefault(name)
                    ?? "(no version)";
                return new PackageRef(name, version);
            })
            .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> GetTargetFrameworks(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var frameworks = ReadTargetFrameworks(document);
        if (frameworks.Count > 0) return frameworks;

        var propsPath = FindNearestAncestorFile(Path.GetDirectoryName(projectPath)!, "Directory.Build.props");
        return propsPath is null ? [] : ReadTargetFrameworks(XDocument.Load(propsPath));
    }

    private static IReadOnlyList<string> ReadTargetFrameworks(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(framework => framework, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyDictionary<string, string> ReadCentralPackageVersions(string projectPath)
    {
        var propsPath = FindNearestAncestorFile(Path.GetDirectoryName(projectPath)!, "Directory.Packages.props");
        if (propsPath is null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var document = XDocument.Load(propsPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .Select(element => new
            {
                Name = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"),
                Version = (string?)element.Attribute("Version")
                    ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Version))
            .GroupBy(item => item.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Version!, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindNearestAncestorFile(string startDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }

    public static bool IsAspNetCoreProject(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var sdk = (string?)document.Root?.Attribute("Sdk") ?? string.Empty;
        return sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
            document.Descendants("FrameworkReference").Any(element =>
                string.Equals((string?)element.Attribute("Include"), "Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase));
    }

    private static string ProjectNameOf(string projectPath) => Path.GetFileNameWithoutExtension(projectPath);

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/") || normalized.Contains("/obj/");
    }
}
