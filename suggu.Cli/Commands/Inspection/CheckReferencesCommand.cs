using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class CheckReferencesCommand : Command<CheckReferencesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-l|--layer <PROJECT>")]
        [Description("Project/layer name. Omit to show the entire solution reference tree.")]
        public string? Layer { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = SolutionLocator.FindSolutionRoot(Directory.GetCurrentDirectory());
        if (root is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a solution");
            return 1;
        }

        var graph = ReferenceInspector.GetReferenceGraph(root);
        if (graph.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]-[/] no projects found");
            return 0;
        }

        var byName = graph.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
        var selected = string.IsNullOrWhiteSpace(settings.Layer)
            ? graph
            : graph.Where(project => project.Name.Equals(settings.Layer, StringComparison.OrdinalIgnoreCase) ||
                project.Name.EndsWith("." + settings.Layer, StringComparison.OrdinalIgnoreCase)).ToList();
        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]x[/] project/layer '{Markup.Escape(settings.Layer!)}' not found");
            return 1;
        }

        var tree = new Tree($"[bold]{Markup.Escape(Path.GetFileName(root))}[/]");
        foreach (var project in selected)
        {
            var node = tree.AddNode(Label(project));
            AddReferences(node, project, byName, [project.Name]);
        }
        AnsiConsole.Write(tree);
        return 0;
    }

    private static string Label(ProjectReferences project) => project.References.Count == 0
        ? $"[blue]{Markup.Escape(project.Name)}[/] [grey](no project references)[/]"
        : $"[blue]{Markup.Escape(project.Name)}[/]";

    private static void AddReferences(IHasTreeNodes parent, ProjectReferences project,
        IReadOnlyDictionary<string, ProjectReferences> byName, HashSet<string> visited)
    {
        foreach (var referenceName in project.References)
        {
            if (!visited.Add(referenceName))
            {
                parent.AddNode($"[red]{Markup.Escape(referenceName)} (circular reference)[/]");
                continue;
            }
            var referenced = byName.GetValueOrDefault(referenceName);
            var node = parent.AddNode(referenced is null
                ? $"[yellow]{Markup.Escape(referenceName)}[/] [grey](outside workspace)[/]"
                : Label(referenced).Replace("blue", "green", StringComparison.Ordinal));
            if (referenced is not null) AddReferences(node, referenced, byName, visited);
            visited.Remove(referenceName);
        }
    }
}
