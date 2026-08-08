using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Inspection;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

internal sealed class AddReferencesCommand : Command<AddReferencesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--from <PROJECT>")]
        [Description("Project that receives the reference.")]
        public string From { get; init; } = string.Empty;

        [CommandOption("-t|--to <PROJECT>")]
        [Description("Referenced project. Repeat the option or use comma-separated names for multiple projects.")]
        public string[] To { get; init; } = [];

        [CommandOption("-r|--remove")]
        [Description("Remove the references instead of adding them.")]
        public bool Remove { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would happen without changing project files.")]
        public bool DryRun { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(From)) return ValidationResult.Error("--from is required");
            if (To.Length == 0 || To.All(string.IsNullOrWhiteSpace)) return ValidationResult.Error("at least one --to project is required");
            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = SolutionLocator.FindSolutionRoot(Directory.GetCurrentDirectory());
        if (root is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a solution");
            return 1;
        }

        var source = ResolveProject(root, settings.From);
        if (source is null) return 1;
        var targetNames = settings.To.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase);
        var operations = new List<Operation>();
        foreach (var targetName in targetNames)
        {
            var target = ResolveProject(root, targetName);
            if (target is null) return 1;
            if (source.ProjectPath.Equals(target.ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]x[/] a project cannot reference itself ({Markup.Escape(source.Name)})");
                return 1;
            }
            operations.Add(settings.Remove
                ? new RemoveReferenceOperation(source.ProjectPath, target.ProjectPath)
                : new AddReferenceOperation(source.ProjectPath, target.ProjectPath));
        }

        var plan = new Plan($"{(settings.Remove ? "remove" : "add")} references from {source.Name}", operations);
        return ReportRenderer.Render(new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun)));
    }

    private static DotnetProject? ResolveProject(string root, string name)
    {
        var project = ProjectInspector.FindProject(root, name);
        if (project is null)
        {
            var available = string.Join(", ", ProjectInspector.GetProjects(root).Select(item => item.Name));
            AnsiConsole.MarkupLine($"[red]x[/] project '{Markup.Escape(name)}' not found. Available: {Markup.Escape(available)}");
        }
        return project;
    }
}
