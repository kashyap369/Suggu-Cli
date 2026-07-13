using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Commands;
using suggu.Cli.Infrastructure;

var version = (Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev")
    .Split('+')[0]; // strip build metadata

// -v / --version before Spectre parsing, so it works regardless of command tree.
if (args is ["-v"] or ["--version"])
{
    AnsiConsole.MarkupLine($"suggu [green]{version}[/]");
    return 0;
}

// "suggu help" is a common reflex — treat it as "suggu --help" instead of an error.
if (args is ["help"])
{
    args = ["--help"];
}

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("suggu");
    config.SetApplicationVersion(version);
    config.SetHelpProvider(new CategorizedHelpProvider(config.Settings));
    config.RegisterCommands();

    config.SetExceptionHandler((ex, _) =>
    {
        if (ex is CommandParseException parse)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(parse.Message)}");
            AnsiConsole.MarkupLine("[grey]Run[/] [blue]suggu --help[/] [grey]to see all available commands and how to use them.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(ex.Message)}");
        return 1;
    });
});

return app.Run(args);
