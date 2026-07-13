namespace suggu.Core.Generation;

/// <summary>
/// The parsed argument of an add-style command. "Worker/User" means Name = "User"
/// nested under Parent = "Worker" (parent may be several segments deep or empty).
/// </summary>
public sealed record GeneratorInput(string Name, string Parent)
{
    /// <summary>Parse "Worker/User" (or just "User", or "A/B/C") into parent + name.</summary>
    public static GeneratorInput Parse(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("name must not be empty", nameof(path));
        }

        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0
            ? new GeneratorInput(normalized, string.Empty)
            : new GeneratorInput(normalized[(lastSlash + 1)..], normalized[..lastSlash]);
    }
}
