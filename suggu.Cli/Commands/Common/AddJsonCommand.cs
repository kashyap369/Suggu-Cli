using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Cli.Infrastructure;
using suggu.Core.Planning;

namespace suggu.Cli.Commands.Common;

internal sealed class AddJsonCommand : Command<AddJsonCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("JSON file name; the .json extension is added when omitted.")]
        public string? Name { get; init; }

        [CommandOption("-l|--layer <PROJECT>")]
        [Description("Project/layer name. When omitted, interactive terminals show a selector.")]
        public string? Layer { get; init; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Folder relative to the selected project, e.g. Configuration/Seed.")]
        public string? Path { get; init; }

        [CommandOption("-f|--force")]
        [Description("Overwrite an existing JSON file.")]
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
                AnsiConsole.MarkupLine("[red]x[/] JSON file name is required in non-interactive use");
                return 1;
            }
            name = AnsiConsole.Ask<string>("JSON file name [grey](e.g. appsettings.Development.json)[/]:");
        }

        var cwd = Directory.GetCurrentDirectory();
        var project = ProjectCommandResolver.Resolve(cwd, settings.Layer, targetPath: null, requireWeb: false);
        if (project is null) return 1;

        try
        {
            var folder = ProjectCommandResolver.ResolveArtifactFolder(project, settings.Path);
            var relative = Path.GetRelativePath(project.ProjectDirectory, folder);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("the target path must be inside the selected .NET project");

            var fileName = Path.GetFileName(name.Trim());
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fileName += ".json";
            var filePath = Path.Combine(folder, fileName);
            var plan = new Plan($"add JSON file {fileName}",
            [
                new CreateFolderOperation(folder),
                new WriteFileOperation(filePath, "built-in:json", $"{{{Environment.NewLine}}}{Environment.NewLine}"),
            ]);
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
