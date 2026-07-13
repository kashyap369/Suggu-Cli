using suggu.Core.Planning;

namespace suggu.Core.Generation;

/// <summary>The project shapes "suggu create project" offers, mapped to dotnet new templates.</summary>
public enum ProjectType
{
    Api,
    Mvc,
}

/// <summary>
/// Plans "create project": a Web API or MVC project via dotnet new, added to the
/// solution when one exists. Same basic project VS creates, minus the GUI.
/// </summary>
public static class ProjectPlanner
{
    public static Plan BuildPlan(
        string? solutionPath,
        string name,
        ProjectType type,
        string? parentDirectory = null,
        string? framework = null,
        bool useControllers = false,
        NewSolution? newSolution = null)
    {
        var root = parentDirectory
            ?? (solutionPath is not null ? Path.GetDirectoryName(solutionPath)!
                : newSolution?.Directory ?? Directory.GetCurrentDirectory());
        var outputDirectory = Path.Combine(root, name);
        var projectPath = Path.Combine(outputDirectory, $"{name}.csproj");

        var template = type switch
        {
            ProjectType.Api => "webapi",
            ProjectType.Mvc => "mvc",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        // "webapi" scaffolds minimal APIs by default; --use-controllers gives the classic shape.
        IReadOnlyList<string>? extraArgs = type == ProjectType.Api && useControllers
            ? ["--use-controllers"]
            : null;

        var operations = new List<Operation>();

        if (solutionPath is null && newSolution is not null)
        {
            operations.Add(new CreateSolutionOperation(newSolution.Directory, newSolution.Name));
        }

        operations.Add(new CreateProjectOperation(template, name, outputDirectory, NormalizeFramework(framework), extraArgs));

        if (solutionPath is not null)
        {
            operations.Add(new AddProjectToSolutionOperation(solutionPath, projectPath));
        }
        else if (newSolution is not null)
        {
            // The solution file's extension isn't known until dotnet creates it; the executor resolves the directory.
            operations.Add(new AddProjectToSolutionOperation(newSolution.Directory, projectPath));
        }

        return new Plan($"create project {name} ({template})", operations);
    }

    /// <summary>A solution to create as part of the plan when none exists yet.</summary>
    public sealed record NewSolution(string Directory, string Name);

    /// <summary>"DummyCliApi.Api" -> "DummyCliApi"; a name with no dot is used as-is.</summary>
    public static string DeriveSolutionName(string projectName)
    {
        var lastDot = projectName.LastIndexOf('.');
        return lastDot > 0 ? projectName[..lastDot] : projectName;
    }

    /// <summary>"8" / "9" / "10" become monikers; "net8.0" passes through; null means SDK default.</summary>
    public static string? NormalizeFramework(string? framework)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            return null;
        }

        var trimmed = framework.Trim();
        return trimmed.StartsWith("net", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"net{trimmed}.0";
    }

    /// <summary>The major version of a normalized framework ("net8.0" -> 8), or null when unparseable.</summary>
    public static int? FrameworkMajor(string? normalizedFramework)
    {
        if (normalizedFramework is null)
        {
            return null;
        }

        var digits = normalizedFramework
            .TrimStart('n', 'e', 't', 'N', 'E', 'T')
            .Split('.')[0];

        return int.TryParse(digits, out var major) ? major : null;
    }
}
