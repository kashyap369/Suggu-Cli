using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Packs;
using suggu.Core.Planning;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.CleanArchitecture;

/// <summary>What the module attaches to each registered generator via WithData().</summary>
internal sealed record GeneratorBinding(GeneratorSpec Generator, IPackFileProvider PackFiles);

/// <summary>
/// The one command class behind every "suggu add &lt;noun&gt;". Which noun it is comes
/// from the pack generator bound at registration — adding a new noun is pack data,
/// never a new command class.
/// </summary>
internal sealed class AddGeneratorCommand : Command<AddGeneratorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name, optionally nested: User or Worker/User (creates the Worker subfolder).")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("-p|--path <SUBFOLDER>")]
        [Description("Subfolder to nest under (e.g. -p Items). Same as prefixing the name: Items/Product.")]
        public string? Path { get; init; }

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

        var cwd = Directory.GetCurrentDirectory();
        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var pack = PackContext.Current.Manifest;
        var layer = LayerResolver.Resolve(solutionRoot, pack, generator.Layer);
        if (layer is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]✗[/] no {Markup.Escape(generator.Layer)} layer found — " +
                $"expected a project matching {Markup.Escape(string.Join(" or ", pack.FindLayer(generator.Layer)?.ProjectPatterns ?? []))}");
            return 1;
        }

        // -p Items + "Product" is the same as "Items/Product"; -p also composes with a nested name.
        var input = GeneratorInput.Parse(string.IsNullOrWhiteSpace(settings.Path)
            ? settings.Name
            : $"{settings.Path.Trim('/', '\\')}/{settings.Name}");
        var plan = GeneratorEngine.BuildPlan(generator, input, layer, binding.PackFiles);

        var report = new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun, settings.Force));
        return ReportRenderer.Render(report);
    }
}
