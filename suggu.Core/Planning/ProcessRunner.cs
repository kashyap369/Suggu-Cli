using System.Diagnostics;

namespace suggu.Core.Planning;

/// <summary>
/// The one place suggu shells out to the dotnet CLI (plan §5.3): uniform output
/// capture and error handling. Mutating calls belong to the executor only;
/// read-only queries (dotnet --list-sdks) may also come from Inspection.
/// </summary>
internal static class ProcessRunner
{
    public sealed record ProcessResult(int ExitCode, string Output)
    {
        public bool Success => ExitCode == 0;
    }

    public static ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to start {fileName}");

        // Read both redirected streams concurrently so a verbose build cannot block
        // because one OS pipe fills while the other is being drained.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        var output = string.Join('\n', new[] { stdoutTask.Result, stderrTask.Result }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ProcessResult(process.ExitCode, output.Trim());
    }
}
