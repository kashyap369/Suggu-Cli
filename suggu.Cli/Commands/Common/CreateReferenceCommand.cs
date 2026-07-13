using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

/// <summary>
/// "suggu create reference api shared domain" — add project references, several at
/// once, with short layer names resolved to real projects (api -> Shop.Api).
/// </summary>
internal sealed class CreateReferenceCommand : Command<CreateReferenceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<project>")]
        [Description("The project that gets the references (short name ok: api, core, Shop.Api).")]
        public string Project { get; init; } = string.Empty;

        [CommandArgument(1, "<references>")]
        [Description("One or more projects to reference (short names ok).")]
        public string[] References { get; init; } = [];

        [CommandOption("--dry-run")]
        [Description("Print the plan without changing anything.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var solutionRoot = SolutionLocator.FindSolutionRoot(Directory.GetCurrentDirectory());
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var source = ResolveProject(solutionRoot, settings.Project);
        if (source is null)
        {
            return 1;
        }

        var operations = new List<Operation>();
        foreach (var referenceName in settings.References)
        {
            var target = ResolveProject(solutionRoot, referenceName);
            if (target is null)
            {
                return 1;
            }

            if (string.Equals(target.ProjectPath, source.ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] a project can't reference itself ({Markup.Escape(source.Name)})");
                return 1;
            }

            operations.Add(new AddReferenceOperation(source.ProjectPath, target.ProjectPath));
        }

        var plan = new Plan($"create reference {settings.Project}", operations);
        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun));
        return ReportRenderer.Render(report);
    }

    private static ProjectLayer? ResolveProject(string solutionRoot, string name)
    {
        var project = LayerInspector.FindLayer(solutionRoot, name);
        if (project is null)
        {
            var available = string.Join(", ", LayerInspector.GetLayers(solutionRoot).Select(l => l.Name));
            AnsiConsole.MarkupLine($"[red]✗[/] project '{Markup.Escape(name)}' not found. Available: {Markup.Escape(available)}");
        }

        return project;
    }
}
