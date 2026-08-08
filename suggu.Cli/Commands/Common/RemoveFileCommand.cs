using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class RemoveFileCommand : Command<RemoveFileCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more files to remove.")]
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
            if (Directory.Exists(fullPath))
            {
                AnsiConsole.MarkupLine($"[yellow]-[/] skipped {Markup.Escape(fullPath)} (is a folder; use remove folder)");
                continue;
            }
            if (!File.Exists(fullPath))
            {
                AnsiConsole.MarkupLine($"[yellow]-[/] skipped {Markup.Escape(fullPath)} (file not found)");
                continue;
            }

            try
            {
                File.Delete(fullPath);
                AnsiConsole.MarkupLine($"[red]removed[/] {Markup.Escape(fullPath)} (not recoverable by suggu)");
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
