using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;

namespace suggu.Cli.Commands.Inspection;

internal static class InspectionCommandsModule
{
    public static IConfigurator AddInspectionCommands(this IConfigurator config)
    {
        config.AddCategorizedBranch("check", CommandCategories.Dotnet, check =>
        {
            check.SetDescription("Inspect and diagnose a .NET solution");
            check.AddCommand<CheckReferencesCommand>("references").WithDescription("Show the full project reference order");
            check.AddCommand<CheckReferencesCommand>("ref").WithDescription("Alias for check references");
            check.AddCommand<CheckBuildCommand>("build").WithDescription("Build and explain compiler/MSBuild diagnostics");
            check.AddCommand<CheckFlowCommand>("flow").WithDescription("Trace an endpoint through connected source methods");
        });

        CommandCategories.Assign("check references", CommandCategories.Dotnet);
        CommandCategories.Assign("check ref", CommandCategories.Dotnet);
        CommandCategories.Assign("check build", CommandCategories.Dotnet);
        CommandCategories.Assign("check flow", CommandCategories.Dotnet);
        return config;
    }
}
