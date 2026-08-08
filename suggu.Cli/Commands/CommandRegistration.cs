using Spectre.Console.Cli;
using suggu.Cli.Commands.Common;
using suggu.Cli.Commands.Inspection;

namespace suggu.Cli.Commands;

/// <summary>
/// The single entry point that wires every command module into the app.
/// Program.cs calls this and nothing else; each module lives beside its own commands.
/// </summary>
internal static class CommandRegistration
{
    public static IConfigurator RegisterCommands(this IConfigurator config)
    {
        config.AddCommonCommands();
        config.AddInspectionCommands();
        return config;
    }
}
