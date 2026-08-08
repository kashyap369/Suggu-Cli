using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Planning;
using suggu.Core.Rulebooks;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

internal sealed class CreateRulebookCommand : Command<CreateRulebookCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--path <PATH>")]
        [Description("Target workspace or a directory inside its solution. Defaults to the current directory.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Replace an existing SUGGU-RULEBOOK.md starter file.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show the target without creating the rulebook.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var start = Path.GetFullPath(settings.Path ?? cwd, cwd);
        var solutionRoot = Directory.Exists(start) ? SolutionLocator.FindSolutionRoot(start) : null;
        var workspaceRoot = solutionRoot ?? start;

        var docs = Path.Combine(workspaceRoot, "docs");
        var file = Path.Combine(docs, RulebookLoader.FileName);
        var plan = new Plan("create Suggu rulebook",
        [
            new CreateFolderOperation(docs),
            new WriteFileOperation(file, "built-in:rulebook", RulebookTemplate.Content),
        ]);
        return ReportRenderer.Render(new PlanExecutor().Execute(plan,
            new ExecutionOptions(settings.DryRun, settings.Force)));
    }
}
