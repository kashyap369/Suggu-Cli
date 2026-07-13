using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class ListPackagesCommand : Command<ListPackagesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-l|--layer <LAYER>")]
        [Description("Layer/project name, case-insensitive (e.g. Cli). Defaults to the current layer.")]
        public string? Layer { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();

        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        ProjectLayer? layer;
        if (string.IsNullOrWhiteSpace(settings.Layer))
        {
            // No layer given: use the project the command is running inside.
            layer = LayerInspector.GetCurrentLayer(cwd);
            if (layer is null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] not inside a project — pass --layer <name>");
                return 1;
            }
        }
        else
        {
            layer = LayerInspector.FindLayer(solutionRoot, settings.Layer);
            if (layer is null)
            {
                var available = string.Join(", ", LayerInspector.GetLayers(solutionRoot).Select(l => l.Name));
                AnsiConsole.MarkupLine($"[red]✗[/] layer '{Markup.Escape(settings.Layer)}' not found. Available: {Markup.Escape(available)}");
                return 1;
            }
        }

        var packages = LayerInspector.GetPackages(layer.ProjectPath);

        AnsiConsole.MarkupLine($"[bold]Layer:[/] [green]{Markup.Escape(layer.Name)}[/]");

        if (packages.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]–[/] no packages installed");
            return 0;
        }

        foreach (var p in packages)
        {
            AnsiConsole.MarkupLine($"  [blue]{Markup.Escape(p.Name)}[/] [grey]{Markup.Escape(p.Version)}[/]");
        }

        return 0;
    }
}
