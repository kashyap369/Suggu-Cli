using suggu.Core.Planning;
using System.Text.RegularExpressions;

namespace suggu.Core.Inspection;

/// <summary>How well the machine can target a requested framework major version.</summary>
public enum FrameworkSupport
{
    /// <summary>An SDK of that exact major version is installed.</summary>
    SdkPresent,

    /// <summary>Only newer SDKs are installed — they can still target the older framework.</summary>
    ViaNewerSdk,

    /// <summary>Every installed SDK is older than the requested framework.</summary>
    NotSupported,
}

/// <summary>What "dotnet --list-sdks" told us. Installed=false means the CLI itself wasn't found.</summary>
public sealed record DotnetInfo(bool Installed, IReadOnlyList<string> SdkVersions, IReadOnlyList<int> SdkMajors);

/// <summary>
/// Read-only preflight checks against the local dotnet CLI, so commands can say
/// "install the .NET 8 SDK" instead of failing halfway through a dotnet new.
/// </summary>
public static class DotnetEnvironment
{
    public static DotnetInfo Inspect()
    {
        try
        {
            var result = ProcessRunner.Run("dotnet", ["--list-sdks"]);
            if (!result.Success)
            {
                return new DotnetInfo(false, [], []);
            }

            var versions = ParseSdkList(result.Output);
            var majors = versions
                .Select(v => int.TryParse(v.Split('.')[0], out var major) ? major : -1)
                .Where(m => m > 0)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            return new DotnetInfo(true, versions, majors);
        }
        catch (Exception)
        {
            // dotnet not on PATH (Win32Exception) or not runnable — same answer either way.
            return new DotnetInfo(false, [], []);
        }
    }

    public static FrameworkSupport Support(DotnetInfo info, int frameworkMajor)
    {
        if (info.SdkMajors.Contains(frameworkMajor))
        {
            return FrameworkSupport.SdkPresent;
        }

        return info.SdkMajors.Any(m => m > frameworkMajor)
            ? FrameworkSupport.ViaNewerSdk
            : FrameworkSupport.NotSupported;
    }

    /// <summary>Framework choices exposed by the active SDK's installed project template.</summary>
    public static IReadOnlyList<string> TemplateFrameworks(string template)
    {
        try
        {
            var result = ProcessRunner.Run("dotnet", ["new", template, "--help"]);
            return result.Success ? ParseTemplateFrameworks(result.Output) : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static IReadOnlyList<string> ParseTemplateFrameworks(string output) =>
        Regex.Matches(output, @"\bnet\d+\.\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(framework => FrameworkMajor(framework))
            .ToList();

    private static int FrameworkMajor(string framework) =>
        int.TryParse(framework.AsSpan(3, framework.IndexOf('.') - 3), out var major) ? major : -1;

    /// <summary>Each line looks like "10.0.100 [C:\Program Files\dotnet\sdk]" — keep the version part.</summary>
    public static IReadOnlyList<string> ParseSdkList(string output) =>
        output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2)[0].Trim())
            .Where(v => v.Length > 0)
            .ToList();
}
