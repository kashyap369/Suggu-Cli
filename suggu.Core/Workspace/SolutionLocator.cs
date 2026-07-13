namespace suggu.Core.Workspace;

/// <summary>
/// Finds the solution by walking up the directory tree from a starting folder.
/// Supports both .slnx and .sln (an agent's working directory isn't always the solution root).
/// </summary>
public static class SolutionLocator
{
    public static string? FindSolutionFile(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var solution = dir.EnumerateFiles("*.slnx")
                .Concat(dir.EnumerateFiles("*.sln"))
                .FirstOrDefault();

            if (solution is not null)
            {
                return solution.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    public static string? FindSolutionRoot(string startDirectory)
    {
        var file = FindSolutionFile(startDirectory);
        return file is null ? null : Path.GetDirectoryName(file);
    }
}
