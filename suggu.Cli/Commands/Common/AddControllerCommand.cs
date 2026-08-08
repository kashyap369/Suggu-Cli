using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Generation;
using suggu.Core.Planning;

namespace suggu.Cli.Commands.Common;

internal sealed class AddControllerCommand : Command<AddControllerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Controller name, with or without the Controller suffix.")]
        public string? Name { get; init; }

        [CommandOption("-t|--type <TYPE>")]
        [Description("Controller type: api or mvc.")]
        public string? Type { get; init; }

        [CommandOption("-l|--layer <PROJECT>")]
        [Description("ASP.NET Core project/layer name when the solution contains multiple projects.")]
        public string? Layer { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Target folder. Defaults to the selected project's Controllers folder.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite an existing controller.")]
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
                AnsiConsole.MarkupLine("[red]x[/] controller name is required in non-interactive use");
                return 1;
            }
            name = AnsiConsole.Ask<string>("Controller name:");
        }

        var requestedType = settings.Type;
        if (string.IsNullOrWhiteSpace(requestedType) && !Console.IsInputRedirected)
        {
            requestedType = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Controller type:")
                .AddChoices("API", "MVC"));
        }
        requestedType ??= "api";
        if (!Enum.TryParse<ControllerType>(requestedType, ignoreCase: true, out var type))
        {
            AnsiConsole.MarkupLine($"[red]x[/] unknown controller type '{Markup.Escape(requestedType)}' - use api or mvc");
            return 1;
        }

        var cwd = Directory.GetCurrentDirectory();
        var project = ProjectCommandResolver.Resolve(cwd, settings.Layer, targetPath: null, requireWeb: true);
        if (project is null) return 1;
        var folder = ProjectCommandResolver.ResolveArtifactFolder(project, settings.Path, "Controllers");
        try
        {
            var plan = ControllerPlanner.BuildPlan(project, name, type, folder);
            return ReportRenderer.Render(new PlanExecutor().Execute(plan, new ExecutionOptions(settings.DryRun, settings.Force)));
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
