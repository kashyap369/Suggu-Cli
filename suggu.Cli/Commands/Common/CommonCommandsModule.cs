using Spectre.Console.Cli;
using suggu.Cli.Commands.Inspection;
using suggu.Cli.Infrastructure;

namespace suggu.Cli.Commands.Common;

internal static class CommonCommandsModule
{
    public static IConfigurator AddCommonCommands(this IConfigurator config)
    {
        config.AddCategorizedBranch("create", CommandCategories.General, create =>
        {
            create.SetDescription("Create filesystem items or .NET projects");
            create.AddCommand<CreateFolderCommand>("folder").WithDescription("Create folders and seed each with .gitignore");
            create.AddCommand<CreateFileCommand>("file").WithDescription("Create files of any type");
            create.AddCommand<CreateProjectCommand>("project").WithDescription("Create an SDK Web API, MVC, or console project");
            create.AddCommand<CreateRulebookCommand>("rulebook").WithDescription("Create docs/SUGGU-RULEBOOK.md for project-specific commands");
        });

        config.AddCategorizedBranch("add", CommandCategories.Dotnet, add =>
        {
            add.SetDescription("Add source artifacts or project references");
            add.AddCommand<AddClassCommand>("class").WithDescription("Add a namespaced C# class");
            add.AddCommand<AddInterfaceCommand>("interface").WithDescription("Add a namespaced C# interface");
            add.AddCommand<AddControllerCommand>("controller").WithDescription("Add an API or MVC controller");
            add.AddCommand<AddLibraryCommand>("library").WithDescription("Add a class library project to the solution");
            add.AddCommand<AddJsonCommand>("json").WithDescription("Add a JSON file inside a selected .NET project");
            add.AddCommand<AddReferencesCommand>("references").WithDescription("Add or remove one or more project references");
            add.AddCommand<AddReferencesCommand>("ref").WithDescription("Alias for add references");
        });

        config.AddCategorizedBranch("remove", CommandCategories.General, remove =>
        {
            remove.SetDescription("Remove filesystem items");
            remove.AddCommand<RemoveFolderCommand>("folder").WithDescription("Remove folders recursively with all contents");
            remove.AddCommand<RemoveFileCommand>("file").WithDescription("Remove one or more files");
        });

        config.AddCategorizedBranch("list", CommandCategories.General, list =>
        {
            list.SetDescription("Inspect directories or .NET project packages");
            list.AddCommand<ListFolderCommand>("folder").WithDescription("List direct folders or a full depth/size overview");
            list.AddCommand<ListPackagesCommand>("packages").WithDescription("List packages installed in a .NET project");
        });

        config.AddCategorizedCommand<FindCommand>("find", CommandCategories.General)
            .WithDescription("Find a file or folder below a directory or solution root");
        config.AddCategorizedCommand<GrepFileCommand>("grep", CommandCategories.Dotnet)
            .WithDescription("Locate a solution file and preview readable content");
        config.AddCategorizedCommand<RulebookHelpCommand>("rulebook", CommandCategories.Dotnet)
            .WithDescription("List and run project-specific commands from docs/SUGGU-RULEBOOK.md");
        config.AddCategorizedBranch("project", CommandCategories.Dotnet, project =>
        {
            project.SetDescription("Inspect the enclosing .NET solution");
            project.AddCommand<ProjectInfoCommand>("info").WithDescription("Show frameworks, dependencies, packages, size, changes, and structure");
        });

        CommandCategories.Assign("create folder", CommandCategories.General);
        CommandCategories.Assign("create file", CommandCategories.General);
        CommandCategories.Assign("remove folder", CommandCategories.General);
        CommandCategories.Assign("remove file", CommandCategories.General);
        CommandCategories.Assign("list folder", CommandCategories.General);
        CommandCategories.Assign("find", CommandCategories.General);
        CommandCategories.Assign("create project", CommandCategories.Dotnet);
        CommandCategories.Assign("create rulebook", CommandCategories.Dotnet);
        CommandCategories.Assign("add class", CommandCategories.Dotnet);
        CommandCategories.Assign("add interface", CommandCategories.Dotnet);
        CommandCategories.Assign("add controller", CommandCategories.Dotnet);
        CommandCategories.Assign("add library", CommandCategories.Dotnet);
        CommandCategories.Assign("add json", CommandCategories.Dotnet);
        CommandCategories.Assign("add references", CommandCategories.Dotnet);
        CommandCategories.Assign("add ref", CommandCategories.Dotnet);
        CommandCategories.Assign("list packages", CommandCategories.Dotnet);
        CommandCategories.Assign("grep", CommandCategories.Dotnet);
        CommandCategories.Assign("rulebook", CommandCategories.Dotnet);
        CommandCategories.Assign("project info", CommandCategories.Dotnet);
        return config;
    }
}
