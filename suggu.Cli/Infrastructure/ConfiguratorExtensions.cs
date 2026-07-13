using Spectre.Console.Cli;

namespace suggu.Cli.Infrastructure;

internal static class ConfiguratorExtensions
{
    /// <summary>
    /// Adds a verb branch and, in the same call, records its help category — so a verb
    /// can never be registered without a category (the two used to drift apart).
    /// </summary>
    public static IBranchConfigurator AddCategorizedBranch(
        this IConfigurator config,
        string name,
        string category,
        Action<IConfigurator<CommandSettings>> action)
    {
        CommandCategories.Assign(name, category);
        return config.AddBranch(name, action);
    }

    /// <summary>Adds a top-level command (no sub-verbs) and records its help category in the same call.</summary>
    public static ICommandConfigurator AddCategorizedCommand<TCommand>(
        this IConfigurator config,
        string name,
        string category)
        where TCommand : class, ICommand
    {
        CommandCategories.Assign(name, category);
        return config.AddCommand<TCommand>(name);
    }
}
