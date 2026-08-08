using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

/// <summary>Creates official SDK Web API, MVC, or console projects with positional and option grammar.</summary>
internal sealed class CreateProjectCommand : Command<CreateProjectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[first]")]
        [Description("Project type (webapi/mvc/console), or project name when --type is used.")]
        public string? First { get; init; }

        [CommandArgument(1, "[second]")]
        [Description("Framework version for positional usage, such as 8, 9, or 10.")]
        public string? Second { get; init; }

        [CommandOption("-n|--name <NAME>")]
        [Description("Project name. If omitted, an interactive prompt or location-based default is used.")]
        public string? Name { get; init; }

        [CommandOption("-t|--type <TYPE>")]
        [Description("Project type: webapi/api, mvc, or console.")]
        public string? Type { get; init; }

        [CommandOption("-f|--framework <VERSION>")]
        [Description("Target framework: 8, 9, 10, or net8.0 style.")]
        public string? Framework { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent folder for the project. Defaults to the current directory.")]
        public string? Path { get; init; }

        [CommandOption("--controllers")]
        [Description("Web API only: use controllers instead of the minimal API template.")]
        public bool Controllers { get; init; }

        [CommandOption("--sln <NAME>")]
        [Description("Solution name to create when none exists.")]
        public string? SolutionName { get; init; }

        [CommandOption("--no-sln")]
        [Description("Create a standalone project when no solution exists.")]
        public bool NoSolution { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show the creation plan without writing anything.")]
        public bool DryRun { get; init; }

        [CommandOption("--open")]
        [Description("Open the created solution/project with the system-associated IDE.")]
        public bool Open { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var dotnet = DotnetEnvironment.Inspect();
        if (!dotnet.Installed)
        {
            AnsiConsole.MarkupLine("[red]x[/] dotnet CLI not found - install the .NET SDK first");
            return 1;
        }

        var parsed = ParseInput(settings);
        if (parsed is null) return 1;
        var (name, type, framework) = parsed.Value;
        var availableFrameworks = DotnetEnvironment.TemplateFrameworks(ProjectPlanner.TemplateFor(type));
        if (string.IsNullOrWhiteSpace(framework) && !Console.IsInputRedirected && availableFrameworks.Count > 0)
        {
            framework = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Target framework:")
                    .PageSize(10)
                    .UseConverter(FrameworkLabel)
                    .AddChoices(availableFrameworks));
        }

        var normalizedFramework = ProjectPlanner.NormalizeFramework(framework);
        if (normalizedFramework is not null && availableFrameworks.Count > 0 &&
            !availableFrameworks.Contains(normalizedFramework, StringComparer.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine(
                $"[red]x[/] {Markup.Escape(normalizedFramework)} is not available for the active {Markup.Escape(ProjectPlanner.TemplateFor(type))} template. " +
                $"Available: {Markup.Escape(string.Join(", ", availableFrameworks))}");
            return 1;
        }
        var major = ProjectPlanner.FrameworkMajor(normalizedFramework);
        if (major is not null && DotnetEnvironment.Support(dotnet, major.Value) == FrameworkSupport.NotSupported)
        {
            AnsiConsole.MarkupLine($"[red]x[/] installed SDKs cannot target .NET {major}. Installed: {Markup.Escape(string.Join(", ", dotnet.SdkVersions))}");
            return 1;
        }

        var cwd = Directory.GetCurrentDirectory();
        var parent = Path.GetFullPath(settings.Path ?? cwd, cwd);
        var solutionPath = SolutionLocator.FindSolutionFile(cwd);
        ProjectPlanner.NewSolution? newSolution = null;
        if (solutionPath is null && !settings.NoSolution)
        {
            var solutionName = settings.SolutionName ?? ProjectPlanner.DeriveSolutionName(name);
            newSolution = new ProjectPlanner.NewSolution(parent, solutionName);
        }

        var plan = ProjectPlanner.BuildPlan(solutionPath, name, type, parent, framework, settings.Controllers, newSolution);
        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun));
        var exitCode = ReportRenderer.Render(report);
        if (exitCode != 0 || settings.DryRun) return exitCode;

        var shouldOpen = settings.Open ||
            (!Console.IsInputRedirected && AnsiConsole.Confirm("Open the created solution/project in your IDE?", false));
        if (!shouldOpen) return exitCode;

        var workspacePath = ResolveWorkspacePath(solutionPath, newSolution, parent, name);
        if (WorkspaceLauncher.TryOpen(workspacePath, out var error))
            AnsiConsole.MarkupLine($"[green]opened[/] {Markup.Escape(workspacePath)}");
        else
            AnsiConsole.MarkupLine($"[yellow]-[/] project was created, but the IDE could not be opened: {Markup.Escape(error ?? "unknown error")}");
        return exitCode;
    }

    private static (string Name, ProjectType Type, string? Framework)? ParseInput(Settings settings)
    {
        var optionType = ParseType(settings.Type);
        var positionalType = ParseType(settings.First);
        ProjectType type;
        string? name;
        string? framework;

        if (optionType is not null)
        {
            type = optionType.Value;
            name = settings.Name ?? settings.First;
            framework = settings.Framework ?? settings.Second;
        }
        else if (positionalType is not null)
        {
            type = positionalType.Value;
            name = settings.Name;
            framework = settings.Framework ?? settings.Second;
        }
        else if (settings.First is null && settings.Type is null && !Console.IsInputRedirected)
        {
            type = AnsiConsole.Prompt(
                new SelectionPrompt<ProjectType>()
                    .Title("Project type:")
                    .UseConverter(ProjectTypeLabel)
                    .AddChoices(ProjectType.Api, ProjectType.Mvc, ProjectType.Console));
            name = settings.Name;
            framework = settings.Framework ?? settings.Second;
        }
        else
        {
            type = ProjectType.Api;
            name = settings.Name ?? settings.First;
            framework = settings.Framework ?? settings.Second;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            if (!Console.IsInputRedirected)
                name = AnsiConsole.Ask<string>("Project name:");
            else
            {
                var baseName = new DirectoryInfo(Path.GetFullPath(settings.Path ?? Directory.GetCurrentDirectory())).Name;
                name = type switch
                {
                    ProjectType.Api => baseName + ".Api",
                    ProjectType.Mvc => baseName + ".Web",
                    ProjectType.Console => baseName + ".Console",
                    _ => baseName,
                };
                AnsiConsole.MarkupLine($"[yellow]-[/] no project name provided; using {Markup.Escape(name)}");
            }
        }

        if (settings.Type is not null && optionType is null)
        {
            AnsiConsole.MarkupLine($"[red]x[/] unknown project type '{Markup.Escape(settings.Type)}' - use webapi, mvc, or console");
            return null;
        }
        return (name.Trim(), type, framework);
    }

    private static ProjectType? ParseType(string? value)
    {
        if (value is null) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "api" or "webapi" => ProjectType.Api,
            "mvc" => ProjectType.Mvc,
            "console" => ProjectType.Console,
            _ => null,
        };
    }

    private static string ProjectTypeLabel(ProjectType type) => type switch
    {
        ProjectType.Api => "Web API",
        ProjectType.Mvc => "MVC",
        ProjectType.Console => "Console",
        _ => type.ToString(),
    };

    private static string FrameworkLabel(string framework) =>
        $".NET {ProjectPlanner.FrameworkMajor(framework)?.ToString() ?? framework} ({framework})";

    private static string ResolveWorkspacePath(
        string? existingSolution,
        ProjectPlanner.NewSolution? newSolution,
        string parent,
        string projectName)
    {
        if (existingSolution is not null) return existingSolution;
        if (newSolution is not null)
        {
            var slnx = Path.Combine(newSolution.Directory, $"{newSolution.Name}.slnx");
            if (File.Exists(slnx)) return slnx;
            var sln = Path.Combine(newSolution.Directory, $"{newSolution.Name}.sln");
            if (File.Exists(sln)) return sln;
        }
        return Path.Combine(parent, projectName, $"{projectName}.csproj");
    }
}
