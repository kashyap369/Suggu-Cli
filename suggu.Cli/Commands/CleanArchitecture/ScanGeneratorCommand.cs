using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.CleanArchitecture;

/// <summary>
/// Runs a scan-mode generator (suggu add repositories): input comes from scanning
/// Entities/**, not from the command line — one output set per entity found,
/// subfolder structure mirrored.
/// </summary>
internal sealed class ScanGeneratorCommand : Command<ScanGeneratorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Print the plan without writing anything.")]
        public bool DryRun { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite files that already exist (default: skip them).")]
        public bool Force { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var binding = (GeneratorBinding)context.Data!;
        var generator = binding.Generator;
        var pack = PackContext.Current.Manifest;

        var cwd = Directory.GetCurrentDirectory();
        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var layer = LayerResolver.Resolve(solutionRoot, pack, generator.Layer);
        if (layer is null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] no {Markup.Escape(generator.Layer)} layer project found");
            return 1;
        }

        var entities = EntityScanner.Scan(layer);
        if (entities.Count == 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]–[/] no entities found under {Markup.Escape(Path.Combine(layer.Directory, EntityScanner.DefaultSourceFolder))}");
            return 0;
        }

        AnsiConsole.MarkupLine($"[grey]found {entities.Count} entities[/]");

        var plan = GeneratorEngine.BuildScanPlan(generator, entities, layer, binding.PackFiles);
        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun, settings.Force));
        return ReportRenderer.Render(report);
    }
}
