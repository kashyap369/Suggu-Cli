using Spectre.Console;
using suggu.Core.Planning;
using suggu.Core.Rulebooks;
using suggu.Core.Workspace;

namespace suggu.Cli.Infrastructure;

internal static class RulebookCommandRunner
{
    public static int Run(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        var rulebookPath = RulebookLoader.Find(cwd);
        if (rulebookPath is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] docs/SUGGU-RULEBOOK.md was not found - run [blue]suggu create rulebook[/] first");
            return 1;
        }

        LoadedRulebook loaded;
        try { loaded = RulebookLoader.Load(rulebookPath); }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"] or ["list"])
        {
            RenderHelp(loaded);
            return 0;
        }
        if (args is ["--check"] or ["check"]) return Check(loaded, cwd);

        var match = FindCommand(loaded.Definition.Commands, args);
        if (match is null)
        {
            AnsiConsole.MarkupLine($"[red]x[/] unknown rulebook command '{Markup.Escape(string.Join(' ', args.Where(arg => !arg.StartsWith('-'))))}'");
            RenderHelp(loaded);
            return 1;
        }

        var (command, consumed) = match.Value;
        var remaining = args[consumed..].ToList();
        if (remaining.Remove("--help") || remaining.Remove("-h"))
        {
            RenderCommandHelp(command);
            return 0;
        }
        var dryRun = remaining.Remove("--dry-run");
        var force = remaining.Remove("--force");

        try
        {
            var values = ReadParameters(command, remaining);
            var workspaceRoot = WorkspaceRootOf(loaded.FilePath);
            var plan = RulebookPlanner.BuildPlan(workspaceRoot, loaded.Definition, command, values);
            AnsiConsole.MarkupLine($"[bold]Rulebook:[/] {Markup.Escape(loaded.FilePath)}");
            AnsiConsole.MarkupLine($"[bold]Command:[/] [blue]{Markup.Escape(command.Name)}[/]");
            return ReportRenderer.Render(new PlanExecutor().Execute(plan, new ExecutionOptions(dryRun, force)));
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OperationCanceledException)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static int Check(LoadedRulebook loaded, string cwd)
    {
        try
        {
            var workspaceRoot = WorkspaceRootOf(loaded.FilePath);
            foreach (var command in loaded.Definition.Commands)
            {
                var samples = command.Parameters.ToDictionary(
                    parameter => parameter.Name,
                    parameter => parameter.Type.Equals("csharp-identifier", StringComparison.OrdinalIgnoreCase) ? "Sample" : "sample",
                    StringComparer.OrdinalIgnoreCase);
                RulebookPlanner.BuildPlan(workspaceRoot, loaded.Definition, command, samples);
            }
            AnsiConsole.MarkupLine($"[green]valid[/] {Markup.Escape(loaded.FilePath)}");
            AnsiConsole.MarkupLine($"[grey]{loaded.Definition.Commands.Count} custom command(s) checked; no files were written.[/]");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static Dictionary<string, string> ReadParameters(
        RulebookCommandDefinition command,
        IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new Queue<string>();
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Enqueue(arg);
                continue;
            }
            var name = arg[2..];
            var parameter = command.Parameters.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"unknown parameter '--{name}' for rulebook command '{command.Name}'");
            if (++index >= args.Count) throw new InvalidDataException($"--{name} requires a value");
            values[parameter.Name] = args[index];
        }

        foreach (var parameter in command.Parameters.Where(parameter => !values.ContainsKey(parameter.Name)))
        {
            if (positionals.Count > 0) values[parameter.Name] = positionals.Dequeue();
            else if (parameter.Required && !Console.IsInputRedirected)
                values[parameter.Name] = AnsiConsole.Ask<string>($"{Markup.Escape(parameter.Name)}:");
        }
        if (positionals.Count > 0)
            throw new InvalidDataException($"too many values for rulebook command '{command.Name}': {string.Join(' ', positionals)}");
        return values;
    }

    private static (RulebookCommandDefinition Command, int Consumed)? FindCommand(
        IReadOnlyList<RulebookCommandDefinition> commands,
        IReadOnlyList<string> args) =>
        commands
            .Select(command => (Command: command, Tokens: command.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(candidate => candidate.Tokens.Length <= args.Count &&
                candidate.Tokens.Select((token, index) => token.Equals(args[index], StringComparison.OrdinalIgnoreCase)).All(match => match))
            .OrderByDescending(candidate => candidate.Tokens.Length)
            .Select(candidate => ((RulebookCommandDefinition Command, int Consumed)?)(candidate.Command, candidate.Tokens.Length))
            .FirstOrDefault();

    private static void RenderHelp(LoadedRulebook loaded)
    {
        AnsiConsole.MarkupLine("[bold blue]Suggu rulebook commands[/]");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(loaded.FilePath)}[/]");
        if (loaded.Definition.Commands.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]-[/] no custom commands are defined");
            return;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Command").AddColumn("Description");
        foreach (var command in loaded.Definition.Commands.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase))
            table.AddRow($"suggu --rulebook {Markup.Escape(command.Name)}", Markup.Escape(command.Description));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Use --dry-run to preview, --force to overwrite, or --check to validate every recipe.[/]");
    }

    private static void RenderCommandHelp(RulebookCommandDefinition command)
    {
        var parameters = string.Join(' ', command.Parameters.Select(parameter => $"<{parameter.Name}>"));
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(command.Description)}[/]");
        AnsiConsole.MarkupLine($"suggu --rulebook {Markup.Escape(command.Name)} {Markup.Escape(parameters)} [[--dry-run]] [[--force]]");
    }

    private static string WorkspaceRootOf(string rulebookPath) =>
        Directory.GetParent(Path.GetDirectoryName(rulebookPath)!)?.FullName
        ?? throw new InvalidDataException("the rulebook must be stored under a docs folder");
}
