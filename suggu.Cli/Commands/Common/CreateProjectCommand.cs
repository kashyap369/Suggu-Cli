using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

/// <summary>
/// "suggu create project Shop.Api -t api -f 9" — the basic Web API / MVC project
/// VS creates, via dotnet new. Preflight-checks the dotnet CLI and the requested
/// framework so failures are clear messages, not half-created projects.
/// </summary>
internal sealed class CreateProjectCommand : Command<CreateProjectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Project name (e.g. Shop.Api).")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("-t|--type <TYPE>")]
        [Description("Project type: api or mvc.")]
        public string Type { get; init; } = "api";

        [CommandOption("--framework <VERSION>")]
        [Description("Target framework: 8, 9, 10 or net8.0 style. Defaults to the SDK default.")]
        public string? Framework { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent folder for the project. Defaults to the solution root (or current directory).")]
        public string? Path { get; init; }

        [CommandOption("--controllers")]
        [Description("API only: scaffold classic controllers instead of minimal APIs.")]
        public bool Controllers { get; init; }

        [CommandOption("--sln <NAME>")]
        [Description("Solution name to create when none exists. Defaults to the project name without its last segment.")]
        public string? SolutionName { get; init; }

        [CommandOption("--no-sln")]
        [Description("Don't create a solution when none exists (standalone project).")]
        public bool NoSolution { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the plan without creating anything.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProjectType>(settings.Type, ignoreCase: true, out var type))
        {
            AnsiConsole.MarkupLine($"[red]✗[/] unknown project type '{Markup.Escape(settings.Type)}' — use api or mvc");
            return 1;
        }

        // Preflight: is the dotnet CLI there at all?
        var dotnet = DotnetEnvironment.Inspect();
        if (!dotnet.Installed)
        {
            AnsiConsole.MarkupLine("[red]✗[/] dotnet CLI not found — install the .NET SDK from https://dotnet.microsoft.com/download");
            return 1;
        }

        // Preflight: can this machine target the requested framework?
        var framework = ProjectPlanner.NormalizeFramework(settings.Framework);
        var major = ProjectPlanner.FrameworkMajor(framework);
        if (major is not null)
        {
            var installed = string.Join(", ", dotnet.SdkVersions);
            switch (DotnetEnvironment.Support(dotnet, major.Value))
            {
                case FrameworkSupport.NotSupported:
                    AnsiConsole.MarkupLine(
                        $"[red]✗[/] no SDK can target .NET {major} — installed SDKs: {Markup.Escape(installed)}. " +
                        $"Install the .NET {major} SDK first.");
                    return 1;

                case FrameworkSupport.ViaNewerSdk:
                    AnsiConsole.MarkupLine(
                        $"[yellow]–[/] no .NET {major} SDK installed (have: {Markup.Escape(installed)}) — " +
                        "a newer SDK will target it instead");
                    break;
            }
        }

        // Inside a solution -> wire the project in. No solution -> create one too (unless --no-sln),
        // so the result is always openable in Visual Studio.
        var solutionPath = SolutionLocator.FindSolutionFile(Directory.GetCurrentDirectory());
        ProjectPlanner.NewSolution? newSolution = null;
        if (solutionPath is null)
        {
            if (settings.NoSolution)
            {
                AnsiConsole.MarkupLine("[grey]no solution found — creating a standalone project (--no-sln)[/]");
            }
            else
            {
                var solutionName = settings.SolutionName ?? ProjectPlanner.DeriveSolutionName(settings.Name);
                newSolution = new ProjectPlanner.NewSolution(Directory.GetCurrentDirectory(), solutionName);
                AnsiConsole.MarkupLine($"[grey]no solution found — creating solution '{Markup.Escape(solutionName)}' too[/]");
            }
        }

        var plan = ProjectPlanner.BuildPlan(
            solutionPath, settings.Name, type, settings.Path, settings.Framework, settings.Controllers, newSolution);

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun));
        return ReportRenderer.Render(report);
    }
}
