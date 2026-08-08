namespace suggu.Core.Inspection;

public sealed record DirectoryEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    IReadOnlyList<DirectoryEntry> Children);

public sealed record FileTypeSummary(string Extension, int FileCount, long TotalBytes);

public sealed record FolderSummary(
    string RelativePath,
    int DirectFileCount,
    int DirectFolderCount,
    long TotalBytes);

public sealed record DirectoryOverview(
    string RootPath,
    int FolderCount,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<FileTypeSummary> FileTypes,
    IReadOnlyList<FolderSummary> Folders);

/// <summary>Generic directory inspection that has no dependency on a .NET workspace.</summary>
public static class DirectoryInspector
{
    public static IReadOnlyList<string> ListFolders(string path) =>
        Directory.EnumerateDirectories(Resolve(path))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static DirectoryEntry BuildTree(string path, int? maxDepth = null)
    {
        if (maxDepth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "depth cannot be negative");
        }

        var root = Resolve(path);
        return BuildEntry(root, depth: 0, maxDepth);
    }

    public static DirectoryOverview GetOverview(string path)
    {
        var root = Resolve(path);
        var files = EnumerateFilesSafe(root).ToList();
        var folders = EnumerateDirectoriesSafe(root).ToList();

        var types = files
            .GroupBy(FileTypeOf, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileTypeSummary(
                group.Key,
                group.Count(),
                group.Sum(FileSizeSafe)))
            .OrderByDescending(type => type.TotalBytes)
            .ThenBy(type => type.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var folderSummaries = new[] { root }
            .Concat(folders)
            .Select(folder => new FolderSummary(
                Path.GetRelativePath(root, folder) is "." ? "." : Path.GetRelativePath(root, folder),
                EnumerateFilesSafe(folder, recursive: false).Count(),
                EnumerateDirectoriesSafe(folder, recursive: false).Count(),
                EnumerateFilesSafe(folder).Sum(FileSizeSafe)))
            .OrderBy(folder => folder.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DirectoryOverview(
            root,
            folders.Count,
            files.Count,
            files.Sum(FileSizeSafe),
            types,
            folderSummaries);
    }

    private static DirectoryEntry BuildEntry(string path, int depth, int? maxDepth)
    {
        if (File.Exists(path))
        {
            return new DirectoryEntry(Path.GetFileName(path), path, false, FileSizeSafe(path), []);
        }

        var children = maxDepth is not null && depth >= maxDepth
            ? []
            : EnumerateEntriesSafe(path)
                .OrderByDescending(Directory.Exists)
                .ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .Select(child => BuildEntry(child, depth + 1, maxDepth))
                .ToList();

        return new DirectoryEntry(
            new DirectoryInfo(path).Name,
            path,
            true,
            children.Sum(child => child.Size),
            children);
    }

    private static string Resolve(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"directory not found: {fullPath}");
        }

        return fullPath;
    }

    private static IEnumerable<string> EnumerateEntriesSafe(string path)
    {
        try { return Directory.EnumerateFileSystemEntries(path).ToList(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string path, bool recursive = true)
    {
        try
        {
            return Directory.EnumerateDirectories(
                path,
                "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string path, bool recursive = true)
    {
        try
        {
            return Directory.EnumerateFiles(
                path,
                "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static long FileSizeSafe(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    private static string FileTypeOf(string path)
    {
        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) ? "(no extension)" : extension.ToLowerInvariant();
    }
}
