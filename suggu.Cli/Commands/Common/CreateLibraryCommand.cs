using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

/// <summary>
/// "suggu create library MyLib" — a C# class library created and wired into the
/// solution in one step. Class libraries are what Web API / MVC solutions consume;
/// --aspnet additionally lets the library itself use ASP.NET Core types.
/// </summary>
internal sealed class CreateLibraryCommand : Command<CreateLibraryCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Library project name (e.g. Shop.Shared).")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent folder for the project. Defaults to the solution root.")]
        public string? Path { get; init; }

        [CommandOption("--framework <VERSION>")]
        [Description("Target framework: 8, 9, 10 or net8.0 style. Defaults to the SDK default.")]
        public string? Framework { get; init; }

        [CommandOption("--aspnet")]
        [Description("Reference Microsoft.AspNetCore.App so the library can use ASP.NET Core (Web API/MVC) types.")]
        public bool AspNet { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the plan without creating anything.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var solutionPath = SolutionLocator.FindSolutionFile(cwd);
        if (solutionPath is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var plan = LibraryPlanner.BuildPlan(solutionPath, settings.Name, settings.Path, settings.Framework, settings.AspNet);

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun));
        return ReportRenderer.Render(report);
    }
}
