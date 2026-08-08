using System.Diagnostics;

namespace suggu.Cli.Infrastructure;

internal static class WorkspaceLauncher
{
    public static bool TryOpen(string path, out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
