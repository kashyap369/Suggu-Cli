using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class CreateFileCommand : Command<CreateFileCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more file names with any extension.")]
        public string[] Names { get; init; } = [];

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent directory. Defaults to the current directory.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite an existing file.")]
        public bool Force { get; init; }

    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = Path.GetFullPath(settings.Path ?? Directory.GetCurrentDirectory());
        foreach (var name in settings.Names)
        {
            var fullPath = Path.GetFullPath(name, basePath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            if (File.Exists(fullPath) && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]-[/] skipped {Markup.Escape(name)} (file already exists)");
                continue;
            }

            File.WriteAllText(fullPath, string.Empty);
            AnsiConsole.MarkupLine($"[green]created[/] {Markup.Escape(fullPath)}");
        }
        return 0;
    }
}
