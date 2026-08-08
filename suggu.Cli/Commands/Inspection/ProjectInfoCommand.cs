using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class ProjectInfoCommand : Command<ProjectInfoCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--max-depth <NUMBER>")]
        [Description("Limit folder-tree rendering; summary totals still inspect the complete source tree.")]
        public int? MaxDepth { get; init; }

        public override ValidationResult Validate() => MaxDepth is < 0
            ? ValidationResult.Error("--max-depth cannot be negative")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var solutionPath = SolutionLocator.FindSolutionFile(Directory.GetCurrentDirectory());
        if (solutionPath is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a .NET solution");
            return 1;
        }

        var report = SolutionInfoInspector.Inspect(solutionPath, settings.MaxDepth);
        RenderSummary(report);
        RenderProjects(report);
        RenderDependencies(report);
        RenderPackages(report);
        RenderTree(report.Tree);
        AnsiConsole.MarkupLine($"[grey]Excluded generated/system folders: {Markup.Escape(string.Join(", ", report.ExcludedFolders))}[/]");
        return 0;
    }

    private static void RenderSummary(SolutionInfoReport report)
    {
        AnsiConsole.Write(new Rule("[bold blue]Solution overview[/]").LeftJustified());
        var summary = new Grid().AddColumn().AddColumn();
        summary.AddRow("[bold]Solution[/]", Markup.Escape(report.SolutionPath));
        summary.AddRow("[bold]Root[/]", Markup.Escape(report.RootPath));
        summary.AddRow("[bold]Projects/layers[/]", report.Projects.Count.ToString());
        summary.AddRow("[bold]Source size[/]", FormatBytes(report.TotalBytes));
        summary.AddRow("[bold]Folders / files[/]", $"{report.FolderCount} / {report.FileCount}");
        summary.AddRow("[bold]Last modified[/]", report.LatestModifiedUtc is null
            ? "[grey]unknown[/]"
            : $"{Markup.Escape(report.LatestModifiedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"))}  [grey]{Markup.Escape(Relative(report.RootPath, report.LatestModifiedFile!))}[/]");
        AnsiConsole.Write(new Panel(summary).Border(BoxBorder.Rounded));
    }

    private static void RenderProjects(SolutionInfoReport report)
    {
        AnsiConsole.Write(new Rule("[bold blue]Projects / layers[/]").LeftJustified());
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Project")
            .AddColumn("Target framework")
            .AddColumn("Files")
            .AddColumn("Size")
            .AddColumn("Latest modified file");
        foreach (var project in report.Projects)
        {
            var frameworks = project.TargetFrameworks.Count == 0
                ? "(inherited/unknown)"
                : string.Join(", ", project.TargetFrameworks);
            var latest = project.LatestModifiedFile is null
                ? "-"
                : $"{Markup.Escape(Relative(Path.GetDirectoryName(project.ProjectPath)!, project.LatestModifiedFile))}\n[grey]{project.LatestModifiedUtc?.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]";
            table.AddRow(
                $"[green]{Markup.Escape(project.Name)}[/]\n[grey]{Markup.Escape(Relative(report.RootPath, project.ProjectPath))}[/]",
                Markup.Escape(frameworks),
                project.FileCount.ToString(),
                FormatBytes(project.TotalBytes),
                latest);
        }
        AnsiConsole.Write(table);
    }

    private static void RenderDependencies(SolutionInfoReport report)
    {
        AnsiConsole.Write(new Rule("[bold blue]Project dependencies[/]").LeftJustified());
        var byName = report.Projects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase);
        var tree = new Tree($"[bold]{Markup.Escape(Path.GetFileNameWithoutExtension(report.SolutionPath))}[/]");
        foreach (var project in report.Projects)
        {
            var node = tree.AddNode($"[blue]{Markup.Escape(project.Name)}[/]");
            AddDependencies(node, project, byName, [project.Name]);
        }
        AnsiConsole.Write(tree);
    }

    private static void AddDependencies(
        IHasTreeNodes parent,
        ProjectInfoDetail project,
        IReadOnlyDictionary<string, ProjectInfoDetail> byName,
        HashSet<string> visited)
    {
        if (project.References.Count == 0)
        {
            parent.AddNode("[grey](no project dependencies)[/]");
            return;
        }
        foreach (var name in project.References)
        {
            if (!visited.Add(name))
            {
                parent.AddNode($"[red]{Markup.Escape(name)} (circular)[/]");
                continue;
            }
            var dependency = byName.GetValueOrDefault(name);
            var node = parent.AddNode(dependency is null
                ? $"[yellow]{Markup.Escape(name)} (outside workspace)[/]"
                : $"[green]{Markup.Escape(name)}[/]");
            if (dependency is not null) AddDependencies(node, dependency, byName, visited);
            visited.Remove(name);
        }
    }

    private static void RenderPackages(SolutionInfoReport report)
    {
        AnsiConsole.Write(new Rule("[bold blue]Packages by project[/]").LeftJustified());
        foreach (var project in report.Projects)
        {
            var table = new Table().Border(TableBorder.Simple).Title($"[green]{Markup.Escape(project.Name)}[/]")
                .AddColumn("Package")
                .AddColumn("Version");
            if (project.Packages.Count == 0) table.AddRow("[grey](none)[/]", "-");
            else
                foreach (var package in project.Packages)
                    table.AddRow(Markup.Escape(package.Name), Markup.Escape(package.Version));
            AnsiConsole.Write(table);
        }
    }

    private static void RenderTree(DirectoryEntry root)
    {
        AnsiConsole.Write(new Rule("[bold blue]Folder and file structure[/]").LeftJustified());
        var tree = new Tree($"[bold blue]{Markup.Escape(root.Name)}/[/] [grey]({FormatBytes(root.Size)})[/]");
        foreach (var child in root.Children) AddTreeNode(tree, child);
        AnsiConsole.Write(tree);
    }

    private static void AddTreeNode(IHasTreeNodes parent, DirectoryEntry entry)
    {
        var label = entry.IsDirectory
            ? $"[blue]{Markup.Escape(entry.Name)}/[/] [grey]({FormatBytes(entry.Size)})[/]"
            : $"{Markup.Escape(entry.Name)} [grey]({FormatBytes(entry.Size)})[/]";
        var node = parent.AddNode(label);
        foreach (var child in entry.Children) AddTreeNode(node, child);
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
