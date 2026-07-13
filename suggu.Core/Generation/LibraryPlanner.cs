using suggu.Core.Planning;

namespace suggu.Core.Generation;

/// <summary>
/// Plans "create library": a classlib project, added to the solution, optionally
/// with the ASP.NET Core framework reference (for libraries that use Web API/MVC types).
/// </summary>
public static class LibraryPlanner
{
    public static Plan BuildPlan(
        string solutionPath,
        string name,
        string? parentDirectory = null,
        string? framework = null,
        bool aspNetCore = false)
    {
        var solutionRoot = Path.GetDirectoryName(solutionPath)!;
        var outputDirectory = Path.Combine(parentDirectory ?? solutionRoot, name);
        var projectPath = Path.Combine(outputDirectory, $"{name}.csproj");

        var operations = new List<Operation>
        {
            new CreateProjectOperation("classlib", name, outputDirectory, ProjectPlanner.NormalizeFramework(framework)),
            new AddProjectToSolutionOperation(solutionPath, projectPath),
        };

        if (aspNetCore)
        {
            operations.Add(new AddFrameworkReferenceOperation(projectPath, "Microsoft.AspNetCore.App"));
        }

        return new Plan($"create library {name}", operations);
    }
}
