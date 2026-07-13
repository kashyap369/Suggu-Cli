using Spectre.Console.Cli;
using suggu.Cli.Commands.Inspection;
using suggu.Cli.Infrastructure;

namespace suggu.Cli.Commands.Common;

/// <summary>Registers the everyday file/folder verbs. Add commands here, not in Program.cs.</summary>
internal static class CommonCommandsModule
{
    public static IConfigurator AddCommonCommands(this IConfigurator config)
    {
        config.AddCategorizedBranch("create", CommandCategories.Common, create =>
        {
            create.SetDescription("Create folders and files");
            create.AddCommand<CreateFolderCommand>("folder").WithDescription("Create one or more folders");
            create.AddCommand<CreateFileCommand>("file").WithDescription("Create one or more files (any type)");
            create.AddCommand<CreateLibraryCommand>("library").WithDescription("Create a class library and add it to the solution");
            create.AddCommand<CreateProjectCommand>("project").WithDescription("Create a Web API or MVC project (like VS does)");
            create.AddCommand<CreateReferenceCommand>("reference").WithDescription("Add project-to-project references");
        });

        config.AddCategorizedBranch("delete", CommandCategories.Common, delete =>
        {
            delete.SetDescription("Delete folders and files");
            delete.AddCommand<DeleteFileCommand>("file").WithDescription("Delete one or more files");
        });

        config.AddCategorizedBranch("list", CommandCategories.Common, list =>
        {
            list.SetDescription("List things.");
            list.AddCommand<ListFolderCommand>("folder").WithDescription("List folders in a directory.");
            list.AddCommand<ListPackagesCommand>("packages").WithDescription("List packages installed in a layer.");
        });

        return config;
    }
}
