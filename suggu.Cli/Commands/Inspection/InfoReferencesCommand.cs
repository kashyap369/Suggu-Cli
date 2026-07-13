using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

/// <summary>
/// "suggu info references" — the project-to-project reference map of the solution,
/// rendered as a tree. Each project's references are expanded recursively, so the
/// full dependency chain (Api → Application → Domain) is visible at a glance.
/// </summary>
internal sealed class InfoReferencesCommand : Command<InfoReferencesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var solutionRoot = SolutionLocator.FindSolutionRoot(Directory.GetCurrentDirectory());
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] no .sln or .slnx found — run this inside a solution");
            return 1;
        }

        var graph = ReferenceInspector.GetReferenceGraph(solutionRoot);
        if (graph.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]–[/] no projects found");
            return 0;
        }

        var byName = graph.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var tree = new Tree($"[bold]{Markup.Escape(Path.GetFileName(solutionRoot))}[/]");
        foreach (var project in graph)
        {
            var label = project.References.Count == 0
                ? $"[blue]{Markup.Escape(project.Name)}[/] [grey](no project references)[/]"
                : $"[blue]{Markup.Escape(project.Name)}[/]";

            var node = tree.AddNode(label);
            AddReferenceNodes(node, project, byName, visited: [project.Name]);
        }

        AnsiConsole.Write(tree);
        return 0;
    }

    private static void AddReferenceNodes(
        TreeNode parent,
        ProjectReferences project,
        IReadOnlyDictionary<string, ProjectReferences> byName,
        HashSet<string> visited)
    {
        foreach (var referenceName in project.References)
        {
            // Cycle guard: MSBuild forbids circular references, but a hand-edited csproj shouldn't hang us.
            if (!visited.Add(referenceName))
            {
                parent.AddNode($"[red]{Markup.Escape(referenceName)} (circular!)[/]");
                continue;
            }

            var node = parent.AddNode($"[green]{Markup.Escape(referenceName)}[/]");

            if (byName.TryGetValue(referenceName, out var referenced))
            {
                AddReferenceNodes(node, referenced, byName, visited);
            }

            visited.Remove(referenceName);
        }
    }
}
