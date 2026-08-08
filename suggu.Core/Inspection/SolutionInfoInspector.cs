namespace suggu.Core.Inspection;

public sealed record ProjectInfoDetail(
    string Name,
    string ProjectPath,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> References,
    IReadOnlyList<PackageRef> Packages,
    long TotalBytes,
    int FileCount,
    string? LatestModifiedFile,
    DateTime? LatestModifiedUtc);

public sealed record SolutionInfoReport(
    string SolutionPath,
    string RootPath,
    IReadOnlyList<ProjectInfoDetail> Projects,
    DirectoryEntry Tree,
    long TotalBytes,
    int FileCount,
    int FolderCount,
    string? LatestModifiedFile,
    DateTime? LatestModifiedUtc,
    IReadOnlyList<string> ExcludedFolders);

/// <summary>Builds a source-focused, architecture-neutral overview of an ordinary .NET solution.</summary>
public static class SolutionInfoInspector
{
    private static readonly HashSet<string> ExcludedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "node_modules", "TestResults",
    };

    public static SolutionInfoReport Inspect(string solutionPath, int? maxDepth = null)
    {
        if (!File.Exists(solutionPath)) throw new FileNotFoundException("solution not found", solutionPath);
        if (maxDepth is < 0) throw new ArgumentOutOfRangeException(nameof(maxDepth), "depth cannot be negative");

        var root = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var allFiles = EnumerateSourceFiles(root).ToList();
        var allFolders = EnumerateSourceFolders(root).ToList();
        var latest = Latest(allFiles);
        var references = ReferenceInspector.GetReferenceGraph(root)
            .ToDictionary(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase);
        var projects = ProjectInspector.GetProjects(root).Select(project =>
        {
            var directory = Path.GetDirectoryName(project.ProjectPath)!;
            var files = EnumerateSourceFiles(directory).ToList();
            var projectLatest = Latest(files);
            var projectReferences = references.GetValueOrDefault(project.ProjectPath)?.References ?? [];
            return new ProjectInfoDetail(
                project.Name,
                project.ProjectPath,
                ProjectInspector.GetTargetFrameworks(project.ProjectPath),
                projectReferences,
                ProjectInspector.GetPackages(project.ProjectPath),
                files.Sum(FileSize),
                files.Count,
                projectLatest.Path,
                projectLatest.ModifiedUtc);
        }).ToList();

        return new SolutionInfoReport(
            Path.GetFullPath(solutionPath),
            root,
            projects,
            BuildTree(root, 0, maxDepth),
            allFiles.Sum(FileSize),
            allFiles.Count,
            allFolders.Count,
            latest.Path,
            latest.ModifiedUtc,
            ExcludedFolderNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static DirectoryEntry BuildTree(string path, int depth, int? maxDepth)
    {
        if (File.Exists(path))
            return new DirectoryEntry(Path.GetFileName(path), path, false, FileSize(path), []);

        var children = maxDepth is not null && depth >= maxDepth
            ? []
            : EnumerateEntries(path)
                .OrderByDescending(Directory.Exists)
                .ThenBy(entry => Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase)
                .Select(entry => BuildTree(entry, depth + 1, maxDepth))
                .ToList();
        var totalBytes = EnumerateSourceFiles(path).Sum(FileSize);
        return new DirectoryEntry(new DirectoryInfo(path).Name, path, true, totalBytes, children);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        foreach (var entry in EnumerateEntries(root))
        {
            if (File.Exists(entry)) yield return entry;
            else
            {
                foreach (var file in EnumerateSourceFiles(entry)) yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateSourceFolders(string root)
    {
        foreach (var directory in EnumerateEntries(root).Where(Directory.Exists))
        {
            yield return directory;
            foreach (var child in EnumerateSourceFolders(directory)) yield return child;
        }
    }

    private static IEnumerable<string> EnumerateEntries(string path)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(path)
                .Where(entry => !IsExcludedDirectory(entry))
                .ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static bool IsExcludedDirectory(string path) =>
        Directory.Exists(path) &&
        (ExcludedFolderNames.Contains(Path.GetFileName(path)) ||
         (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);

    private static long FileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return 0; }
    }

    private static (string? Path, DateTime? ModifiedUtc) Latest(IReadOnlyList<string> files)
    {
        var latest = files
            .Select(path => (Path: path, ModifiedUtc: LastWriteUtc(path)))
            .Where(item => item.ModifiedUtc is not null)
            .OrderByDescending(item => item.ModifiedUtc)
            .FirstOrDefault();
        return latest.Path is null ? (null, null) : latest;
    }

    private static DateTime? LastWriteUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return null; }
    }
}
