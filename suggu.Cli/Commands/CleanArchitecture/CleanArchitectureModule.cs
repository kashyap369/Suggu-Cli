using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Packs;

namespace suggu.Cli.Commands.CleanArchitecture;

/// <summary>
/// Registers the clean-architecture command set from the loaded pack.
/// Every generator entry in pack.json becomes a "suggu add <name>" command
/// automatically — this module never changes when a new generator is added.
/// </summary>
internal static class CleanArchitectureModule
{
    public static IConfigurator AddCleanArchitectureCommands(this IConfigurator config)
    {
        var (pack, files) = PackContext.Current;

        // Every generator entry in the pack becomes an "add" sub-command automatically.
        if (pack.Generators.Count > 0)
        {
            config.AddCategorizedBranch("add", CommandCategories.CleanArchitecture, add =>
            {
                add.SetDescription($"Generate {pack.Name} scaffolds (from the pack)");

                foreach (var generator in pack.Generators)
                {
                    // Scan-mode generators take no <name> argument; input-mode ones do.
                    var command = generator.Mode == GeneratorMode.EntityScan
                        ? add.AddCommand<ScanGeneratorCommand>(generator.Name)
                        : (ICommandConfigurator)add.AddCommand<AddGeneratorCommand>(generator.Name);

                    command
                        .WithDescription($"{generator.Description} [{generator.Layer}]")
                        .WithData(new GeneratorBinding(generator, files));
                }
            });
        }

        // "setup <layer> [section]" — sections come from the pack's seeds.
        if (pack.Seeds.Count > 0)
        {
            config.AddCategorizedCommand<SetupLayerCommand>("setup", CommandCategories.CleanArchitecture)
                .WithDescription("Set up a layer's canonical folders + seed files from the pack");
        }

        return config;
    }
}
