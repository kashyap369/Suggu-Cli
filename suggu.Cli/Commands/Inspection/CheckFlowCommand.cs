using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using suggu.Core.Inspection;
using suggu.Core.Workspace;

namespace suggu.Cli.Commands.Inspection;

internal sealed class CheckFlowCommand : Command<CheckFlowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-c|--controller <NAME>")]
        [Description("Controller name, with or without the Controller suffix.")]
        public string Controller { get; init; } = string.Empty;

        [CommandOption("-m|--method <NAME>")]
        [Description("Action method name to trace.")]
        public string Method { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Controller)) return ValidationResult.Error("--controller is required");
            if (string.IsNullOrWhiteSpace(Method)) return ValidationResult.Error("--method is required");
            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = SolutionLocator.FindSolutionRoot(Directory.GetCurrentDirectory());
        if (root is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] no .sln or .slnx found - run this inside a .NET solution");
            return 1;
        }

        var result = EndpointFlowInspector.Trace(root, settings.Controller, settings.Method);
        if (!result.Found || result.Root is null)
        {
            AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(result.Error ?? "endpoint not found")}");
            return 1;
        }

        var tree = new Tree(Label(result.Root));
        foreach (var child in result.Root.Children)
        {
            AddNode(tree, child);
        }
        AnsiConsole.Write(tree);
        AnsiConsole.MarkupLine("[grey]Only calls resolved to workspace source files are shown; framework/package calls are omitted. Runtime DI, reflection, middleware, filters, and dynamic dispatch may add steps.[/]");
        return 0;
    }

    private static void AddNode(IHasTreeNodes parent, EndpointFlowStep step)
    {
        var node = parent.AddNode(Label(step));
        foreach (var child in step.Children)
        {
            AddNode(node, child);
        }
    }

    private static string Label(EndpointFlowStep step)
    {
        var color = step.Certainty switch
        {
            FlowCertainty.Confirmed => "green",
            FlowCertainty.Ambiguous => "yellow",
            _ => "red",
        };
        var location = $"{step.FilePath}:{step.Line}";
        var note = step.Note is null ? "" : $" [grey]- {Markup.Escape(step.Note)}[/]";
        return $"[{color}]{Markup.Escape(step.Description)}[/] [grey]({Markup.Escape(location)})[/]{note}";
    }
}
