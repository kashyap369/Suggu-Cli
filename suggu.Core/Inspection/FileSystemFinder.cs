using System.Text.RegularExpressions;

namespace suggu.Core.Inspection;

public enum FileSystemEntryType
{
    File,
    Folder,
}

public sealed record FileSystemMatch(string Name, string FullPath, FileSystemEntryType Type);

/// <summary>Recursively finds files or folders below an explicit root.</summary>
public static class FileSystemFinder
{
    public static IReadOnlyList<FileSystemMatch> Find(string root, string query, FileSystemEntryType type)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"directory not found: {fullRoot}");
        }
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("search name cannot be empty", nameof(query));
        }

        var matcher = CreateMatcher(query.Trim());
        var entries = type == FileSystemEntryType.File
            ? Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            : Directory.EnumerateDirectories(fullRoot, "*", SearchOption.AllDirectories);

        return entries
            .Where(path => matcher(Path.GetFileName(path)))
            .Select(path => new FileSystemMatch(Path.GetFileName(path), path, type))
            .OrderBy(match => match.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> FindFilesByExactName(string root, string fileName) =>
        Find(root, fileName, FileSystemEntryType.File)
            .Where(match => match.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            .Select(match => match.FullPath)
            .ToList();

    public static IReadOnlyList<string> FindFilesByName(string root, string fileName)
    {
        var query = fileName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("file name cannot be empty", nameof(fileName));
        }

        var hasExtension = Path.HasExtension(query) || query[0] == '.';
        return Directory.EnumerateFiles(Path.GetFullPath(root), "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                var candidate = hasExtension ? name : Path.GetFileNameWithoutExtension(name);
                return candidate.Equals(query, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Func<string, bool> CreateMatcher(string query)
    {
        if (query.IndexOfAny(['*', '?']) >= 0)
        {
            var expression = "^" + Regex.Escape(query).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return regex.IsMatch;
        }

        return name => name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
