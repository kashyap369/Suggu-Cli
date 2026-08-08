using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Planning;

namespace suggu.Cli.Commands.Common;

internal abstract class AddCodeArtifactCommand(CodeArtifactType artifactType)
    : Command<AddCodeArtifactCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("C# type name. Interfaces receive an I prefix automatically.")]
        public string? Name { get; init; }

        [CommandOption("-l|--layer <PROJECT>")]
        [Description("Project/layer name. When omitted, interactive terminals ask for one.")]
        public string? Layer { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Target folder inside a project.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite an existing file.")]
        public bool Force { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would happen without writing anything.")]
        public bool DryRun { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var name = settings.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            if (Console.IsInputRedirected)
            {
                AnsiConsole.MarkupLine("[red]x[/] name is required in non-interactive use");
                return 1;
            }
            name = AnsiConsole.Ask<string>(artifactType == CodeArtifactType.Class ? "Class name:" : "Interface name:");
        }

        var cwd = Directory.GetCurrentDirectory();
        var project = ProjectCommandResolver.Resolve(cwd, settings.Layer, targetPath: null, requireWeb: false);
        if (project is null) return 1;

        try
        {
            var folder = ProjectCommandResolver.ResolveArtifactFolder(project, settings.Path);
            var plan = CodeArtifactPlanner.BuildPlan(project, project.ProjectDirectory, name, artifactType, folder);
            return ReportRenderer.Render(new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun, settings.Force)));
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]-[/] creation cancelled");
            return 0;
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}

internal sealed class AddClassCommand() : AddCodeArtifactCommand(CodeArtifactType.Class);
internal sealed class AddInterfaceCommand() : AddCodeArtifactCommand(CodeArtifactType.Interface);
