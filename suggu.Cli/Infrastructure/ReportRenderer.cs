using Spectre.Console;
using suggu.Core.Planning;

namespace suggu.Cli.Infrastructure;

/// <summary>
/// Renders an <see cref="ExecutionReport"/> as human console output.
/// One report shape, one renderer — every command's output looks the same.
/// </summary>
internal static class ReportRenderer
{
    /// <summary>Print the report and return the process exit code (0 success, 1 any failure).</summary>
    public static int Render(ExecutionReport report)
    {
        if (report.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]dry-run[/] — nothing was written");
        }

        foreach (var result in report.Results)
        {
            var path = Markup.Escape(result.Operation.TargetPath);
            var line = result.Status switch
            {
                OperationStatus.Created => $"[green]✓ created[/] {path}",
                OperationStatus.Overwritten => $"[blue]↻ overwritten[/] {path}",
                OperationStatus.Skipped => $"[yellow]–[/] skipped {path} [grey]({Markup.Escape(result.Message ?? "exists")})[/]",
                OperationStatus.Failed => $"[red]✗ failed[/] {path} — {Markup.Escape(result.Message ?? "unknown error")}",
                _ => path,
            };

            AnsiConsole.MarkupLine(line);
        }

        var summary =
            $"created {report.Created.Count()}, skipped {report.Skipped.Count()}, " +
            $"overwritten {report.Overwritten.Count()}, failed {report.Failed.Count()}";
        AnsiConsole.MarkupLine($"[grey]{summary}[/]");

        return report.Success ? 0 : 1;
    }
}
