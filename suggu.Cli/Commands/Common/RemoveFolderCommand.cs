using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class RemoveFolderCommand : Command<RemoveFolderCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more folders to remove recursively with all contents.")]
        public string[] Names { get; init; } = [];

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent directory. Defaults to the current directory.")]
        public string? Path { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = Path.GetFullPath(settings.Path ?? Directory.GetCurrentDirectory());
        var failed = false;
        foreach (var name in settings.Names)
        {
            var fullPath = Path.GetFullPath(name, basePath);
            var root = Path.GetPathRoot(fullPath);
            if (fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                failed = true;
                AnsiConsole.MarkupLine($"[red]x[/] refusing to remove protected target {Markup.Escape(fullPath)}");
                continue;
            }
            if (File.Exists(fullPath))
            {
                AnsiConsole.MarkupLine($"[yellow]-[/] skipped {Markup.Escape(fullPath)} (is a file; use remove file)");
                continue;
            }
            if (!Directory.Exists(fullPath))
            {
                AnsiConsole.MarkupLine($"[yellow]-[/] skipped {Markup.Escape(fullPath)} (folder not found)");
                continue;
            }

            try
            {
                Directory.Delete(fullPath, recursive: true);
                AnsiConsole.MarkupLine($"[red]removed[/] {Markup.Escape(fullPath)} and all contents (not recoverable by suggu)");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed = true;
                AnsiConsole.MarkupLine($"[red]x[/] failed to remove {Markup.Escape(fullPath)} - {Markup.Escape(ex.Message)}");
            }
        }
        return failed ? 1 : 0;
    }
}
