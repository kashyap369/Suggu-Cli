using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class ListPackagesCommand : Command<ListPackagesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project <PROJECT>")]
        [Description("Project name, case-insensitive. Defaults to the current project.")]
        public string? Project { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var solutionRoot = SolutionLocator.FindSolutionRoot(cwd);
        if (solutionRoot is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a solution");
            return 1;
        }

        DotnetProject? project;
        if (string.IsNullOrWhiteSpace(settings.Project))
        {
            project = ProjectInspector.GetCurrentProject(cwd);
            if (project is null)
            {
                AnsiConsole.MarkupLine("[red]x[/] not inside a project - pass --project <name>");
                return 1;
            }
        }
        else
        {
            project = ProjectInspector.FindProject(solutionRoot, settings.Project);
            if (project is null)
            {
                var available = string.Join(", ", ProjectInspector.GetProjects(solutionRoot).Select(item => item.Name));
                AnsiConsole.MarkupLine($"[red]x[/] project '{Markup.Escape(settings.Project)}' not found. Available: {Markup.Escape(available)}");
                return 1;
            }
        }

        var packages = ProjectInspector.GetPackages(project.ProjectPath);
        AnsiConsole.MarkupLine($"[bold]Project:[/] [green]{Markup.Escape(project.Name)}[/]");
        if (packages.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]-[/] no packages installed");
            return 0;
        }

        foreach (var package in packages)
        {
            AnsiConsole.MarkupLine($"  [blue]{Markup.Escape(package.Name)}[/] [grey]{Markup.Escape(package.Version)}[/]");
        }
        return 0;
    }
}
