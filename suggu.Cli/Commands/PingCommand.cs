using Spectre.Console;
using Spectre.Console.Cli;

namespace suggu.Cli.Commands;

internal sealed class PingCommand : Command<PingCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]pong[/] — suggu.Cli is wired up.");
        return 0;
    }
}
