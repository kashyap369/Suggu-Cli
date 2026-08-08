using System.Text.RegularExpressions;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Core.Generation;

public enum CodeArtifactType
{
    Class,
    Interface,
}

/// <summary>Creates generic C# class/interface plans using project and folder-derived namespaces.</summary>
public static partial class CodeArtifactPlanner
{
    public static Plan BuildPlan(
        ProjectContext project,
        string currentDirectory,
        string name,
        CodeArtifactType artifactType,
        string? targetPath = null)
    {
        var cleanName = Path.GetFileNameWithoutExtension(name.Trim());
        var alreadyHasInterfacePrefix = cleanName.Length > 1 &&
            cleanName[0] == 'I' && char.IsUpper(cleanName[1]);
        if (artifactType == CodeArtifactType.Interface && !alreadyHasInterfacePrefix)
        {
            cleanName = "I" + cleanName;
        }

        if (!IdentifierRegex().IsMatch(cleanName))
        {
            throw new ArgumentException($"'{cleanName}' is not a valid C# type name", nameof(name));
        }

        var targetDirectory = string.IsNullOrWhiteSpace(targetPath)
            ? Path.GetFullPath(currentDirectory)
            : Path.GetFullPath(targetPath, currentDirectory);

        var nameSpace = NamespaceFor(project, targetDirectory);
        var declaration = artifactType == CodeArtifactType.Class
            ? $"public class {cleanName}"
            : $"public interface {cleanName}";
        var content = $"namespace {nameSpace};{Environment.NewLine}{Environment.NewLine}{declaration}{Environment.NewLine}{{{Environment.NewLine}}}{Environment.NewLine}";
        var filePath = Path.Combine(targetDirectory, cleanName + ".cs");

        return new Plan(
            $"create {artifactType.ToString().ToLowerInvariant()} {cleanName}",
            [new CreateFolderOperation(targetDirectory), new WriteFileOperation(filePath, "built-in:csharp", content)]);
    }

    public static string NamespaceFor(ProjectContext project, string targetDirectory)
    {
        var relative = Path.GetRelativePath(project.ProjectDirectory, Path.GetFullPath(targetDirectory));
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("the target path must be inside the selected .NET project", nameof(targetDirectory));

        var suffix = relative is "." or ""
            ? string.Empty
            : "." + string.Join('.', relative.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries).Select(SanitizeNamespacePart));
        return project.RootNamespace + suffix;
    }

    private static string SanitizeNamespacePart(string part)
    {
        var cleaned = InvalidNamespaceCharacterRegex().Replace(part, "_");
        return cleaned.Length > 0 && char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
    }

    [GeneratedRegex(@"^@?[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_]")]
    private static partial Regex InvalidNamespaceCharacterRegex();
}
