using System.Text.RegularExpressions;
using suggu.Core.Planning;

namespace suggu.Core.Inspection;

public enum BuildDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record BuildDiagnostic(
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? File,
    int? Line,
    int? Column,
    string? Project);

public sealed record BuildInspectionResult(
    bool Success,
    string Target,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    string RawOutput);

/// <summary>Runs dotnet build and parses compiler/MSBuild diagnostics without hiding raw output.</summary>
public static partial class BuildInspector
{
    public static BuildInspectionResult Inspect(string target, bool noRestore = false)
    {
        var arguments = new List<string> { "build", target, "--nologo", "--consoleLoggerParameters:NoSummary" };
        if (noRestore)
        {
            arguments.Add("--no-restore");
        }

        var result = ProcessRunner.Run("dotnet", arguments, WorkingDirectoryOf(target));
        return new BuildInspectionResult(
            result.Success,
            target,
            ParseDiagnostics(result.Output),
            result.Output);
    }

    public static IReadOnlyList<BuildDiagnostic> ParseDiagnostics(string output)
    {
        var diagnostics = new List<BuildDiagnostic>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = DiagnosticRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            diagnostics.Add(new BuildDiagnostic(
                match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                    ? BuildDiagnosticSeverity.Error
                    : BuildDiagnosticSeverity.Warning,
                match.Groups["code"].Value,
                match.Groups["message"].Value.Trim(),
                EmptyToNull(match.Groups["file"].Value.Trim()),
                ParseNumber(match.Groups["line"].Value),
                ParseNumber(match.Groups["column"].Value),
                EmptyToNull(match.Groups["project"].Value.Trim())));
        }

        return diagnostics
            .DistinctBy(d => (d.Severity, d.Code, d.Message, d.File, d.Line, d.Column, d.Project))
            .ToList();
    }

    public static string Explain(BuildDiagnostic diagnostic) => diagnostic.Code switch
    {
        "CS0246" => "A type or namespace could not be resolved. Check its using directive, project reference, package reference, and spelling.",
        "CS0103" => "A name is not available in the current scope. Check spelling and where the variable or member is declared.",
        "CS1061" => "The target type does not contain the called member. Check the receiver type, missing extension-method using, or package version.",
        "CS1503" => "An argument does not match the parameter type expected by the called method.",
        "CS8618" => "A non-nullable member is not initialized before construction completes.",
        "CS0168" => "A variable is declared but never used.",
        "NETSDK1045" => "The installed .NET SDK does not support the requested target framework.",
        "NU1101" => "NuGet could not find the requested package in the configured package sources.",
        _ => "See the original compiler or MSBuild message above; no reliable additional explanation is available.",
    };

    private static string WorkingDirectoryOf(string target) =>
        Directory.Exists(target) ? target : Path.GetDirectoryName(Path.GetFullPath(target))!;

    private static int? ParseNumber(string value) => int.TryParse(value, out var number) ? number : null;
    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    [GeneratedRegex(@"^(?:(?<file>.+?)(?:\((?<line>\d+)(?:,(?<column>\d+))?\))?\s*:\s*)?(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+):\s*(?<message>.*?)(?:\s+\[(?<project>[^\]]+)\])?$", RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosticRegex();
}
