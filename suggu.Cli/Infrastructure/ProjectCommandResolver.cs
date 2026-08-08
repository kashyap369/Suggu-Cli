using Spectre.Console;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Infrastructure;

internal static class ProjectCommandResolver
{
    public static ProjectContext? Resolve(string cwd, string? projectName, string? targetPath, bool requireWeb)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var target = Path.GetFullPath(targetPath, cwd);
            var owningProject = ProjectLocator.FindProject(target);
            if (owningProject is null)
            {
                AnsiConsole.MarkupLine("[red]x[/] the target path is not inside a .NET project");
                return null;
            }
            return ValidateWeb(owningProject, requireWeb);
        }

        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            if (solutionRoot is null)
            {
                AnsiConsole.MarkupLine("[red]x[/] no solution found for --layer/--project selection");
                return null;
            }
            var selected = ProjectInspector.FindProject(solutionRoot, projectName);
            if (selected is null)
            {
                AnsiConsole.MarkupLine($"[red]x[/] project '{Markup.Escape(projectName)}' was not found");
                return null;
            }
            return ValidateWeb(ToContext(selected), requireWeb);
        }

        var current = ProjectLocator.FindProject(cwd);
        var candidates = solutionRoot is null
            ? []
            : ProjectInspector.GetProjects(solutionRoot)
                .Where(project => !requireWeb || ProjectInspector.IsAspNetCoreProject(project.ProjectPath))
                .ToList();

        if (!Console.IsInputRedirected && candidates.Count > 0)
        {
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select the project/layer:")
                .PageSize(12)
                .AddChoices(candidates.Select(project => project.Name)));
            return ValidateWeb(ToContext(candidates.Single(project => project.Name == choice)), requireWeb);
        }

        if (current is not null) return ValidateWeb(current, requireWeb);
        if (candidates.Count == 1) return ValidateWeb(ToContext(candidates[0]), requireWeb);

        AnsiConsole.MarkupLine("[red]x[/] project is ambiguous; pass --layer <name> or --path <folder>");
        return null;
    }

    public static string ResolveArtifactFolder(
        ProjectContext project,
        string? requestedPath,
        string? suggestedFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            return Path.IsPathRooted(requestedPath)
                ? Path.GetFullPath(requestedPath)
                : Path.GetFullPath(requestedPath, project.ProjectDirectory);
        if (Console.IsInputRedirected)
        {
            var fallback = string.IsNullOrWhiteSpace(suggestedFolder)
                ? project.ProjectDirectory
                : Path.Combine(project.ProjectDirectory, suggestedFolder);
            AnsiConsole.MarkupLine($"[yellow]-[/] no folder provided; the file will be created in {Markup.Escape(fallback)}");
            return fallback;
        }

        var prompt = new TextPrompt<string>(
            $"Folder inside [blue]{Markup.Escape(Path.GetFileNameWithoutExtension(project.ProjectPath))}[/] " +
            "[grey](relative, e.g. Features/Orders/Commands; empty = project root)[/]:")
            .AllowEmpty();
        if (!string.IsNullOrWhiteSpace(suggestedFolder)) prompt.DefaultValue(suggestedFolder);
        var folder = AnsiConsole.Prompt(prompt);
        if (string.IsNullOrWhiteSpace(folder))
        {
            var proceed = AnsiConsole.Confirm($"No folder was provided. Create the file in {Markup.Escape(project.ProjectDirectory)}?");
            if (!proceed) throw new OperationCanceledException("creation cancelled");
            return project.ProjectDirectory;
        }
        return Path.GetFullPath(folder, project.ProjectDirectory);
    }

    private static ProjectContext? ValidateWeb(ProjectContext project, bool required)
    {
        if (!required || ProjectInspector.IsAspNetCoreProject(project.ProjectPath)) return project;
        AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(project.ProjectPath)} is not an ASP.NET Core Web project");
        return null;
    }

    private static ProjectContext ToContext(DotnetProject project) => new(
        project.ProjectPath,
        Path.GetDirectoryName(project.ProjectPath)!,
        ProjectLocator.ReadRootNamespace(project.ProjectPath));
}
