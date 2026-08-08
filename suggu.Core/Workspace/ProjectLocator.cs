using System.Xml.Linq;

namespace suggu.Core.Workspace;

public sealed record ProjectContext(string ProjectPath, string ProjectDirectory, string RootNamespace);

/// <summary>Locates the .NET project that owns a directory and derives its root namespace.</summary>
public static class ProjectLocator
{
    public static string? FindProjectFile(string startPath)
    {
        var fullPath = Path.GetFullPath(startPath);
        var directory = File.Exists(fullPath)
            ? new DirectoryInfo(Path.GetDirectoryName(fullPath)!)
            : new DirectoryInfo(NearestExistingDirectory(fullPath));

        while (directory is not null)
        {
            var project = directory.EnumerateFiles("*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return project.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static ProjectContext? FindProject(string startPath)
    {
        var projectPath = FindProjectFile(startPath);
        if (projectPath is null)
        {
            return null;
        }

        return new ProjectContext(
            projectPath,
            Path.GetDirectoryName(projectPath)!,
            ReadRootNamespace(projectPath));
    }

    public static string ReadRootNamespace(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var configured = document.Descendants("RootNamespace").FirstOrDefault()?.Value
            ?? document.Descendants("AssemblyName").FirstOrDefault()?.Value;

        return string.IsNullOrWhiteSpace(configured)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : configured.Trim();
    }

    private static string NearestExistingDirectory(string path)
    {
        var current = path;
        while (!Directory.Exists(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                return Directory.GetCurrentDirectory();
            }
            current = parent;
        }
        return current;
    }
}
