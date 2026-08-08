using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;

namespace suggu.Cli.Commands.Common;

/// <summary>Advertises rulebook mode in built-in help; Program routes its free-form arguments.</summary>
internal sealed class RulebookHelpCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken) =>
        RulebookCommandRunner.Run([]);
}
