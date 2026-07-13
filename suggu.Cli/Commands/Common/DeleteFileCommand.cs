using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class DeleteFileCommand : Command<DeleteFileCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more file names to delete (e.g. notes.txt Models/User.cs).")]
        public string[] Names { get; init; } = [];

        [CommandOption("-p|--path <PATH>")]
        [Description("Folder to delete the files from. Defaults to current directory.")]
        public string? Path { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = settings.Path ?? Directory.GetCurrentDirectory();

        foreach (var name in settings.Names)
        {
            var full = System.IO.Path.Combine(basePath, name);

            // Refuse to remove a directory through a "delete file" command.
            if (Directory.Exists(full))
            {
                AnsiConsole.MarkupLine($"[yellow]–[/] skipped {Markup.Escape(name)} (is a folder, not a file)");
                continue;
            }

            if (!File.Exists(full))
            {
                AnsiConsole.MarkupLine($"[yellow]–[/] skipped {Markup.Escape(name)} (file not found)");
                continue;
            }

            try
            {
                File.Delete(full);
                AnsiConsole.MarkupLine($"[red]🗑 deleted[/] {Markup.Escape(name)}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ failed[/] {Markup.Escape(name)} — {Markup.Escape(ex.Message)}");
            }
        }

        return 0;
    }
}
