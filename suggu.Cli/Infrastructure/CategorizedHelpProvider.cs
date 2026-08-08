using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace suggu.Cli.Infrastructure;

/// <summary>Groups individual command paths into General and .NET sections at root help.</summary>
internal sealed class CategorizedHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    public override IEnumerable<IRenderable> GetCommands(ICommandModel model, ICommandInfo? command)
    {
        if (command is not null) return base.GetCommands(model, command);
        var commands = model.Commands.Where(item => !item.IsHidden).ToList();
        if (commands.Count == 0) return base.GetCommands(model, command);

        var output = new List<IRenderable>
        {
            new Markup(Environment.NewLine),
            new Markup("[yellow]COMMANDS:[/]"),
        };

        foreach (var category in CommandCategories.DisplayOrder)
        {
            var rows = BuildRows(commands, category).ToList();
            if (rows.Count == 0) continue;

            output.Add(new Markup(Environment.NewLine));
            output.Add(new Markup($"  [bold underline]{Markup.Escape(category)}[/]"));
            output.Add(new Markup(Environment.NewLine));
            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadLeft(4).PadRight(4).NoWrap());
            grid.AddColumn();
            foreach (var row in rows) grid.AddRow(row.Name, row.Description);
            output.Add(grid);
        }

        output.Add(new Markup(Environment.NewLine));
        output.Add(new Markup("[grey]Run[/] [blue]suggu <command> --help[/] [grey]for full details.[/]"));
        return output;
    }

    private static IEnumerable<(IRenderable Name, IRenderable Description)> BuildRows(
        IReadOnlyList<ICommandInfo> commands, string category)
    {
        foreach (var top in commands)
        {
            if (!top.IsBranch)
            {
                if (CommandCategories.Of(top.Name) != category) continue;
                yield return (new Markup($"[blue]{Markup.Escape(top.Name)}[/]"),
                    new Markup(Markup.Escape(top.Description ?? string.Empty)));
                yield return (new Markup(string.Empty), new Markup($"[grey]{Markup.Escape(Usage(top, null))}[/]"));
                continue;
            }

            var children = top.Commands.Where(child => !child.IsHidden &&
                CommandCategories.Of($"{top.Name} {child.Name}") == category).ToList();
            if (children.Count == 0) continue;

            yield return (new Markup($"[blue]{Markup.Escape(top.Name)}[/]"),
                new Markup(Markup.Escape(top.Description ?? string.Empty)));
            foreach (var child in children)
            {
                yield return (new Markup($"  [grey]{Markup.Escape(top.Name)}[/] [blue]{Markup.Escape(child.Name)}[/]"),
                    new Markup(Markup.Escape(child.Description ?? string.Empty)));
                yield return (new Markup(string.Empty), new Markup($"[grey]{Markup.Escape(Usage(child, top.Name))}[/]"));
            }
        }
    }

    private static string Usage(ICommandInfo command, string? parent)
    {
        var parts = new List<string> { "suggu" };
        if (parent is not null) parts.Add(parent);
        parts.Add(command.Name);

        foreach (var argument in command.Parameters.OfType<ICommandArgument>().Where(item => !item.IsHidden).OrderBy(item => item.Position))
        {
            var name = argument.Value.Trim('<', '>', '[', ']');
            parts.Add(argument.IsRequired ? $"<{name}>" : $"[{name}]");
        }
        foreach (var option in command.Parameters.OfType<ICommandOption>().Where(item => !item.IsHidden))
        {
            var shortName = option.ShortNames.FirstOrDefault();
            var longName = option.LongNames.FirstOrDefault();
            if (longName is "help" or "version") continue;
            var flag = (shortName, longName) switch
            {
                (not null, not null) => $"-{shortName}|--{longName}",
                (not null, null) => $"-{shortName}",
                _ => $"--{longName}",
            };
            parts.Add($"[{flag}{(option.IsFlag ? string.Empty : $" <{option.ValueName ?? "VALUE"}>")}]");
        }
        return string.Join(' ', parts);
    }
}
