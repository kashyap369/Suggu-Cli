using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;

namespace suggu.Cli.Commands.Common;

internal sealed class ListFolderCommand : Command<ListFolderCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--path|--find-in <PATH>")]
        [Description("Directory to inspect. Defaults to the current directory.")]
        public string? Path { get; init; }

        [CommandOption("-d|--depth")]
        [Description("Show the recursive tree and detailed size/count overview.")]
        public bool Depth { get; init; }

        [CommandOption("--max-depth <NUMBER>")]
        [Description("Limit tree recursion while using --depth.")]
        public int? MaxDepth { get; init; }

        public override ValidationResult Validate() => MaxDepth is < 0
            ? ValidationResult.Error("--max-depth cannot be negative")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var path = settings.Path ?? Directory.GetCurrentDirectory();
        try
        {
            if (!settings.Depth)
            {
                RenderFolders(DirectoryInspector.ListFolders(path));
                return 0;
            }

            RenderTree(DirectoryInspector.BuildTree(path, settings.MaxDepth));
            RenderOverview(DirectoryInspector.GetOverview(path));
            return 0;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static void RenderFolders(IReadOnlyList<string> folders)
    {
        if (folders.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]-[/] no folders here");
            return;
        }
        foreach (var folder in folders)
        {
            AnsiConsole.MarkupLine($"[blue][[dir]][/] {Markup.Escape(Path.GetFileName(folder))}");
        }
    }

    private static void RenderTree(DirectoryEntry root)
    {
        var tree = new Tree($"[bold blue]{Markup.Escape(root.Name)}[/]");
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

    private static void RenderOverview(DirectoryOverview overview)
    {
        AnsiConsole.MarkupLine($"[bold]Directory:[/] {Markup.Escape(overview.RootPath)}");
        AnsiConsole.MarkupLine($"[bold]Folders:[/] {overview.FolderCount}  [bold]Files:[/] {overview.FileCount}  [bold]Size:[/] {FormatBytes(overview.TotalBytes)}");

        var types = new Table().Border(TableBorder.Rounded).AddColumn("Type").AddColumn("Files").AddColumn("Size");
        foreach (var type in overview.FileTypes)
            types.AddRow(Markup.Escape(type.Extension), type.FileCount.ToString(), FormatBytes(type.TotalBytes));
        AnsiConsole.Write(types);

        var folders = new Table().Border(TableBorder.Rounded).AddColumn("Folder").AddColumn("Files").AddColumn("Subfolders").AddColumn("Total size");
        foreach (var folder in overview.Folders)
            folders.AddRow(Markup.Escape(folder.RelativePath), folder.DirectFileCount.ToString(), folder.DirectFolderCount.ToString(), FormatBytes(folder.TotalBytes));
        AnsiConsole.Write(folders);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
