using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace suggu.Cli.Infrastructure;

/// <summary>
/// Renders the root "suggu --help" command list grouped by category
/// (Common / Architecture / Inspection / Diagnosis). Sub-command help
/// (e.g. "suggu create --help") falls back to the default rendering.
/// </summary>
internal sealed class CategorizedHelpProvider : HelpProvider
{
    public CategorizedHelpProvider(ICommandAppSettings settings)
        : base(settings)
    {
    }

    public override IEnumerable<IRenderable> GetCommands(ICommandModel model, ICommandInfo? command)
    {
        // Only customise the root help. Command-specific help uses the default.
        if (command is not null)
        {
            return base.GetCommands(model, command);
        }

        var commands = model.Commands.Where(c => !c.IsHidden).ToList();
        if (commands.Count == 0)
        {
            return base.GetCommands(model, command);
        }

        var output = new List<IRenderable>
        {
            new Markup(Environment.NewLine),
            new Markup("[yellow]COMMANDS:[/]"),
        };

        foreach (var category in CommandCategories.DisplayOrder)
        {
            var inCategory = commands
                .Where(c => CommandCategories.Of(c.Name) == category)
                .ToList();

            if (inCategory.Count == 0)
            {
                continue;
            }

            // Category header on its own line.
            output.Add(new Markup(Environment.NewLine));
            output.Add(new Markup($"  [bold underline]{category}[/]"));
            output.Add(new Markup(Environment.NewLine));

            // Commands in this category as an indented name/description grid.
            // Each branch verb is listed, then its sub-commands ("variants") beneath it.
            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadLeft(4).PadRight(4).NoWrap());
            grid.AddColumn();

            foreach (var c in inCategory)
            {
                // The branch verb itself (e.g. "create"). A branch-less command (setup) gets a usage line too.
                grid.AddRow(
                    new Markup($"[blue]{Markup.Escape(c.Name)}[/]"),
                    new Markup(Markup.Escape(c.Description ?? string.Empty)));

                if (!c.IsBranch)
                {
                    AddUsageRow(grid, Usage(c, parent: null));
                }

                // Its sub-commands, shown as "create folder", indented one level deeper.
                foreach (var child in c.Commands.Where(x => !x.IsHidden))
                {
                    grid.AddRow(
                        new Markup($"  [grey]{Markup.Escape(c.Name)}[/] [blue]{Markup.Escape(child.Name)}[/]"),
                        new Markup(Markup.Escape(child.Description ?? string.Empty)));

                    AddUsageRow(grid, Usage(child, c.Name));
                }
            }

            output.Add(grid);
        }

        output.Add(new Markup(Environment.NewLine));
        output.Add(new Markup("[grey]Run[/] [blue]suggu <command> --help[/] [grey]for full details of any command.[/]"));

        return output;
    }

    private static void AddUsageRow(Grid grid, string usage)
    {
        grid.AddRow(
            new Markup(string.Empty),
            new Markup($"[grey]{Markup.Escape(usage)}[/]"));
    }

    /// <summary>"suggu create folder <names> [-p|--path <PATH>]" — built from the command's real parameters.</summary>
    private static string Usage(ICommandInfo command, string? parent)
    {
        var parts = new List<string> { "suggu" };
        if (parent is not null)
        {
            parts.Add(parent);
        }

        parts.Add(command.Name);

        foreach (var argument in command.Parameters.OfType<ICommandArgument>().Where(a => !a.IsHidden).OrderBy(a => a.Position))
        {
            var name = argument.Value.Trim('<', '>', '[', ']');
            parts.Add(argument.IsRequired ? $"<{name}>" : $"[{name}]");
        }

        foreach (var option in command.Parameters.OfType<ICommandOption>().Where(o => !o.IsHidden))
        {
            var shortName = option.ShortNames.FirstOrDefault();
            var longName = option.LongNames.FirstOrDefault();
            if (longName is "help" or "version")
            {
                continue;
            }

            var flag = (shortName, longName) switch
            {
                (not null, not null) => $"-{shortName}|--{longName}",
                (not null, null) => $"-{shortName}",
                _ => $"--{longName}",
            };

            var value = option.IsFlag ? string.Empty : $" <{option.ValueName ?? "VALUE"}>";
            parts.Add($"[{flag}{value}]");
        }

        return string.Join(' ', parts);
    }
}
