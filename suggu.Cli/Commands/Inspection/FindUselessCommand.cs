using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Inspection;

internal sealed class FindUselessCommand : Command<FindUselessCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--path <PATH>")]
        [Description("Directory to scan. Defaults to current directory.")]
        public string? Path { get; init; }

        [CommandOption("--folders")]
        [Description("Report empty folders.")]
        public bool Folders { get; init; }

        [CommandOption("--files")]
        [Description("Report empty files. (coming soon)")]
        public bool Files { get; init; }

        [CommandOption("-d|--delete")]
        [Description("Delete the empty folders that are found. Default: off (report only).")]
        public bool Delete { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = settings.Path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(basePath))
        {
            AnsiConsole.MarkupLine($"[red]✗[/] path not found: {basePath}");
            return 1;
        }

        if (settings.Files)
        {
            AnsiConsole.MarkupLine("[yellow]–[/] --files is not implemented yet");
        }

        // Default to scanning folders when the user gives no explicit target.
        var scanFolders = settings.Folders || !settings.Files;
        if (!scanFolders)
        {
            return 0;
        }

        var empties = Directory
            .EnumerateDirectories(basePath, "*", SearchOption.AllDirectories)
            .Where(dir => !Directory.EnumerateFileSystemEntries(dir).Any())
            .ToList();

        if (empties.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓[/] no empty folders found");
            return 0;
        }

        var verb = settings.Delete ? "deleting" : "found";
        AnsiConsole.MarkupLine($"[yellow]{empties.Count}[/] empty folder(s) {verb}:");

        var deleted = 0;
        foreach (var dir in empties)
        {
            var rel = System.IO.Path.GetRelativePath(basePath, dir);

            if (!settings.Delete)
            {
                AnsiConsole.MarkupLine($"  [grey]📁[/] {Markup.Escape(rel)}");
                continue;
            }

            try
            {
                Directory.Delete(dir); // safe: the folder is empty
                deleted++;
                AnsiConsole.MarkupLine($"  [red]🗑 deleted[/] {Markup.Escape(rel)}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]✗ failed[/] {Markup.Escape(rel)} — {Markup.Escape(ex.Message)}");
            }
        }

        if (settings.Delete)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] deleted {deleted} of {empties.Count}");
        }

        return 0;
    }
}
