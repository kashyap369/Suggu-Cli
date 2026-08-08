using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class CheckBuildCommand : Command<CheckBuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project <PATH>")]
        [Description("Project or solution to build. Defaults to the enclosing solution, then project.")]
        public string? Project { get; init; }

        [CommandOption("--no-restore")]
        [Description("Do not restore packages before building.")]
        public bool NoRestore { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var target = ResolveTarget(cwd, settings.Project);
        if (target is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln, .slnx, or .csproj found - run this inside a .NET workspace or pass --project");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]building {Markup.Escape(target)}[/]");
        var result = BuildInspector.Inspect(target, settings.NoRestore);

        foreach (var diagnostic in result.Diagnostics)
        {
            var color = diagnostic.Severity == BuildDiagnosticSeverity.Error ? "red" : "yellow";
            var location = diagnostic.File is null
                ? diagnostic.Project ?? target
                : $"{diagnostic.File}{(diagnostic.Line is null ? "" : $":{diagnostic.Line}:{diagnostic.Column ?? 0}")}";
            AnsiConsole.MarkupLine($"[{color}]{diagnostic.Severity.ToString().ToLowerInvariant()} {Markup.Escape(diagnostic.Code)}[/] {Markup.Escape(location)}");
            AnsiConsole.MarkupLine($"  {Markup.Escape(diagnostic.Message)}");
            AnsiConsole.MarkupLine($"  [grey]Why: {Markup.Escape(BuildInspector.Explain(diagnostic))}[/]");
        }

        if (result.Success)
        {
            AnsiConsole.MarkupLine(result.Diagnostics.Count == 0
                ? "[green]check[/] build succeeded with no diagnostics"
                : "[green]check[/] build succeeded");
            return 0;
        }

        if (result.Diagnostics.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]x build failed[/] - raw dotnet output follows:");
            AnsiConsole.WriteLine(result.RawOutput);
        }
        return 1;
    }

    private static string? ResolveTarget(string cwd, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var path = Path.GetFullPath(requested, cwd);
            return File.Exists(path) || Directory.Exists(path) ? path : null;
        }

        return SolutionLocator.FindSolutionFile(cwd) ?? ProjectLocator.FindProjectFile(cwd);
    }
}
