using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class GrepFileCommand : Command<GrepFileCommand.Settings>
{
    private static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".txt", ".json", ".xml", ".md", ".yaml", ".yml", ".config", ".props", ".targets", ".csproj", ".sln", ".slnx", ".gitignore",
    };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--file <NAME>")]
        [Description("File name to inspect. The extension is optional and matching is case-insensitive.")]
        public string File { get; init; } = string.Empty;

        [CommandOption("-p|--path <PATH>")]
        [Description("Search root. Defaults to the enclosing solution root.")]
        public string? Path { get; init; }

        public override ValidationResult Validate() => string.IsNullOrWhiteSpace(File)
            ? ValidationResult.Error("--file is required")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var root = settings.Path is null ? SolutionLocator.FindSolutionRoot(cwd) : Path.GetFullPath(settings.Path, cwd);
        if (root is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no solution found - run inside a solution or pass --path");
            return 1;
        }

        var matches = FileSystemFinder.FindFilesByName(root, settings.File);
        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]-[/] '{Markup.Escape(settings.File)}' was not found under {Markup.Escape(root)}");
            return 0;
        }

        string selected;
        if (matches.Count == 1) selected = matches[0];
        else if (!Console.IsInputRedirected)
            selected = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Multiple files found. Select one:").PageSize(15).AddChoices(matches));
        else
        {
            AnsiConsole.MarkupLine("[yellow]multiple files found; interactive selection is unavailable:[/]");
            foreach (var match in matches) AnsiConsole.WriteLine(match);
            return 0;
        }

        var extension = Path.GetExtension(selected);
        if (!ReadableExtensions.Contains(extension) && !Path.GetFileName(selected).Equals(".gitignore", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(extension.TrimStart('.').ToUpperInvariant())} preview is not supported.[/]");
            AnsiConsole.MarkupLine($"Open this path with the relevant viewer: {Markup.Escape(selected)}");
            return 0;
        }

        var lines = File.ReadAllLines(selected);
        var width = Math.Max(1, lines.Length.ToString().Length);
        var content = string.Join(Environment.NewLine, lines.Select((line, index) => $"{(index + 1).ToString().PadLeft(width)} | {line}"));
        AnsiConsole.Write(new Panel(new Text(content)).Header(Path.GetFileName(selected)).Expand());
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(selected)}[/]");
        return 0;
    }
}
