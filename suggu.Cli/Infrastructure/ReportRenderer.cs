using Spectre.Console;
using suggu.Core.Planning;

namespace suggu.Cli.Infrastructure;

internal static class ReportRenderer
{
    public static int Render(ExecutionReport report)
    {
        if (report.DryRun) AnsiConsole.MarkupLine("[yellow]dry-run[/] - nothing was written");

        foreach (var result in report.Results)
        {
            var path = Markup.Escape(result.Operation.TargetPath);
            var line = result.Status switch
            {
                OperationStatus.Created => $"[green]created[/] {path}",
                OperationStatus.Overwritten => $"[blue]overwritten[/] {path}",
                OperationStatus.Deleted => $"[red]deleted[/] {path}",
                OperationStatus.Skipped => $"[yellow]-[/] skipped {path} [grey]({Markup.Escape(result.Message ?? "exists")})[/]",
                OperationStatus.Failed => $"[red]x failed[/] {path} - {Markup.Escape(result.Message ?? "unknown error")}",
                _ => path,
            };
            AnsiConsole.MarkupLine(line);
        }

        AnsiConsole.MarkupLine(
            $"[grey]created {report.Created.Count()}, skipped {report.Skipped.Count()}, " +
            $"overwritten {report.Overwritten.Count()}, deleted {report.Deleted.Count()}, failed {report.Failed.Count()}[/]");
        return report.Success ? 0 : 1;
    }
}
