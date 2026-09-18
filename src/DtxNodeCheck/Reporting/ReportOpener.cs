using System.Diagnostics;
using DtxNodeCheck.Diagnostics;

namespace DtxNodeCheck.Reporting;

internal static class ReportOpener
{
    public static void Open(string reportPath)
    {
        var fullPath = Path.GetFullPath(reportPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Report non trovato.", fullPath);
        }

        DebugLog.Info("Apertura report richiesta.", new Dictionary<string, string?>
        {
            ["path"] = fullPath
        });

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }
}
