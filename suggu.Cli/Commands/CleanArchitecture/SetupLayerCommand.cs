using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.CleanArchitecture;

/// <summary>
/// "suggu setup domain common" — bring a layer (or one of its sections) to the pack's
/// canonical shape: folders + seed files. Which sections exist and what they contain
/// is pack data; this command never changes when a section is added.
/// </summary>
internal sealed class SetupLayerCommand : Command<SetupLayerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<layer>")]
        [Description("Layer to set up (domain, application, infrastructure, api).")]
        public string Layer { get; init; } = string.Empty;

        [CommandArgument(1, "[section]")]
        [Description("One section (e.g. common). Omit to apply every section the pack defines for the layer.")]
        public string? Section { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print the plan without writing anything.")]
        public bool DryRun { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite files that already exist (default: skip them).")]
        public bool Force { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (pack, files) = PackContext.Current;

        var cwd = Directory.GetCurrentDirectory();
        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var layer = LayerResolver.Resolve(solutionRoot, pack, settings.Layer);
        if (layer is null)
        {
            var known = string.Join(", ", pack.Layers.Select(l => l.Name));
            AnsiConsole.MarkupLine(
                $"[red]✗[/] no '{Markup.Escape(settings.Layer)}' layer project found. Pack layers: {Markup.Escape(known)}");
            return 1;
        }

        var seeds = pack.FindSeeds(layer.LayerName, settings.Section);
        if (seeds.Count == 0)
        {
            var available = string.Join(", ", pack.FindSeeds(layer.LayerName).Select(s => s.Section));
            AnsiConsole.MarkupLine(settings.Section is null
                ? $"[yellow]–[/] the pack defines no setup sections for {Markup.Escape(layer.LayerName)} yet"
                : $"[red]✗[/] unknown section '{Markup.Escape(settings.Section)}' for {Markup.Escape(layer.LayerName)}. Available: {Markup.Escape(available)}");
            return settings.Section is null ? 0 : 1;
        }

        var plans = seeds.Select(s => GeneratorEngine.BuildSeedPlan(s, layer, files)).ToArray();
        var plan = Plan.Combine($"setup {layer.LayerName}", plans);

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun, settings.Force));
        return ReportRenderer.Render(report);
    }
}
