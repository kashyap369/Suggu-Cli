using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

/// <summary>Adds a C# class-library project to the enclosing solution.</summary>
internal sealed class AddLibraryCommand : Command<AddLibraryCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Library project name (e.g. Shop.Shared).")]
        public string? Name { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent folder for the project. Defaults to the solution root.")]
        public string? Path { get; init; }

        [CommandOption("-f|--framework <VERSION>")]
        [Description("Target framework: 8, 9, 10, or net8.0 style.")]
        public string? Framework { get; init; }

        [CommandOption("--aspnet")]
        [Description("Add Microsoft.AspNetCore.App so the library can use ASP.NET Core types.")]
        public bool AspNet { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show the plan without creating anything.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var solutionPath = SolutionLocator.FindSolutionFile(cwd);
        if (solutionPath is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a solution");
            return 1;
        }

        var name = settings.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            if (Console.IsInputRedirected)
            {
                AnsiConsole.MarkupLine("[red]x[/] library name is required in non-interactive use");
                return 1;
            }
            name = AnsiConsole.Ask<string>("Library name [grey](e.g. Shop.Shared)[/]:");
        }

        var availableFrameworks = DotnetEnvironment.TemplateFrameworks("classlib");
        var recommended = RecommendedFramework(Path.GetDirectoryName(solutionPath)!, cwd, availableFrameworks);
        var framework = settings.Framework;
        if (string.IsNullOrWhiteSpace(framework) && !Console.IsInputRedirected && availableFrameworks.Count > 0)
        {
            var choices = availableFrameworks
                .OrderByDescending(item => item.Equals(recommended, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(item => ProjectPlanner.FrameworkMajor(item))
                .ToList();
            framework = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Target framework:")
                .UseConverter(item => FrameworkLabel(item, recommended))
                .AddChoices(choices));
        }

        var normalized = ProjectPlanner.NormalizeFramework(framework);
        if (normalized is not null && availableFrameworks.Count > 0 &&
            !availableFrameworks.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(normalized)} is unavailable for class libraries. Available: {Markup.Escape(string.Join(", ", availableFrameworks))}");
            return 1;
        }

        var plan = LibraryPlanner.BuildPlan(solutionPath, name, settings.Path, framework, settings.AspNet);
        return ReportRenderer.Render(new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun)));
    }

    private static string? RecommendedFramework(
        string solutionRoot,
        string cwd,
        IReadOnlyList<string> available)
    {
        var current = ProjectInspector.GetCurrentProject(cwd);
        var frameworks = current is not null
            ? ProjectInspector.GetTargetFrameworks(current.ProjectPath)
            : ProjectInspector.GetProjects(solutionRoot)
                .SelectMany(project => ProjectInspector.GetTargetFrameworks(project.ProjectPath))
                .GroupBy(framework => framework, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .ToList();
        return frameworks.FirstOrDefault(framework => available.Contains(framework, StringComparer.OrdinalIgnoreCase));
    }

    private static string FrameworkLabel(string framework, string? recommended)
    {
        var label = $".NET {ProjectPlanner.FrameworkMajor(framework)?.ToString() ?? framework} ({framework})";
        return framework.Equals(recommended, StringComparison.OrdinalIgnoreCase)
            ? label + " (recommended - matches project)"
            : label;
    }
}
