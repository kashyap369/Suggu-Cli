using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class CreateFileCommand : Command<CreateFileCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more file names, any extension (e.g. notes.txt User.cs data.xlsx .keep).")]
        public string[] Names { get; init; } = [];

        [CommandOption("-p|--path <PATH>")]
        [Description("Folder to create the files in. Defaults to current directory.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite the file if it already exists.")]
        public bool Force { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = settings.Path ?? Directory.GetCurrentDirectory();

        foreach (var name in settings.Names)
        {
            var full = System.IO.Path.Combine(basePath, name);

            // Support nested paths like Models/User.cs by creating the parent folder first.
            var parent = System.IO.Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (File.Exists(full) && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]–[/] skipped {Markup.Escape(name)} (file already exists)");
                continue;
            }

            // Create (or overwrite) an empty file and release the handle immediately.
            File.Create(full).Dispose();
            AnsiConsole.MarkupLine($"[green]✓ created[/] {Markup.Escape(name)}");
        }

        return 0;
    }
}
