using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Common;

internal sealed class FindCommand : Command<FindCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--file <NAME>")]
        [Description("File name or wildcard pattern to find.")]
        public string? File { get; init; }

        [CommandOption("--folder <NAME>")]
        [Description("Folder name or wildcard pattern to find.")]
        public string? Folder { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Search root. Defaults to the current directory.")]
        public string? Path { get; init; }

        [CommandOption("--sln-search")]
        [Description("Search from the enclosing .NET solution root when --path is omitted.")]
        public bool SolutionSearch { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(File) == string.IsNullOrWhiteSpace(Folder))
                return ValidationResult.Error("provide exactly one of --file or --folder");
            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var root = settings.Path is not null
            ? Path.GetFullPath(settings.Path, cwd)
            : settings.SolutionSearch
                ? SolutionLocator.FindSolutionRoot(cwd)
                : cwd;
        if (root is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found for --sln-search");
            return 1;
        }

        try
        {
            var type = settings.File is not null ? FileSystemEntryType.File : FileSystemEntryType.Folder;
            var matches = FileSystemFinder.Find(root, settings.File ?? settings.Folder!, type);
            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]-[/] no matches found");
                return 0;
            }
            foreach (var match in matches)
                AnsiConsole.MarkupLine($"[green]{match.Type.ToString().ToLowerInvariant()}[/] {Markup.Escape(match.FullPath)}");
            return 0;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
