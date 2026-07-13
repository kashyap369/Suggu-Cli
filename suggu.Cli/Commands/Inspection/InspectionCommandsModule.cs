using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;

namespace suggu.Cli.Commands.Inspection;

/// <summary>Registers the inspection verbs (find, later: info/check). Add commands here, not in Program.cs.</summary>
internal static class InspectionCommandsModule
{
    public static IConfigurator AddInspectionCommands(this IConfigurator config)
    {
        config.AddCategorizedBranch("find", CommandCategories.Inspection, find =>
        {
            find.SetDescription("Find things worth cleaning up.");
            find.AddCommand<FindUselessCommand>("useless").WithDescription("Find empty folders (later: files).");
        });

        config.AddCategorizedBranch("info", CommandCategories.Inspection, info =>
        {
            info.SetDescription("Inspect the solution.");
            info.AddCommand<InfoReferencesCommand>("references")
                .WithDescription("Show which project references which, as a tree.");
        });

        return config;
    }
}
