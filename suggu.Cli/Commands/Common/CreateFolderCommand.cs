using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands.Common;

internal sealed class CreateFolderCommand : Command<CreateFolderCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<names>")]
        [Description("One or more folder names, including nested paths.")]
        public string[] Names { get; init; } = [];

        [CommandOption("-p|--path <PATH>")]
        [Description("Parent directory. Defaults to the current directory.")]
        public string? Path { get; init; }

    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var basePath = Path.GetFullPath(settings.Path ?? Directory.GetCurrentDirectory());
        foreach (var name in settings.Names)
        {
            var fullPath = Path.GetFullPath(name, basePath);
            var existed = Directory.Exists(fullPath);
            Directory.CreateDirectory(fullPath);
            var gitIgnore = Path.Combine(fullPath, ".gitignore");
            if (!File.Exists(gitIgnore))
            {
                File.WriteAllText(gitIgnore, string.Empty);
            }

            AnsiConsole.MarkupLine(existed
                ? $"[yellow]-[/] folder already exists; ensured {Markup.Escape(gitIgnore)}"
                : $"[green]created[/] {Markup.Escape(fullPath)} with .gitignore");
        }
        return 0;
    }
}
