using System.Text;
using Wisegar.DTXInspector.Diagnostics;

namespace Wisegar.DTXInspector.Reporting;

internal static class ReportWriter
{
    public static void Write(string reportPath, string content)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            throw new ArgumentException("Errore: --report richiede un percorso valido.", nameof(reportPath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            DebugLog.Info("Creazione directory report richiesta da --report.", new Dictionary<string, string?>
            {
                ["directory"] = directory
            });
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, content, Encoding.UTF8);
        DebugLog.Info("Report scritto.", new Dictionary<string, string?>
        {
            ["path"] = Path.GetFullPath(reportPath),
            ["bytes"] = Encoding.UTF8.GetByteCount(content).ToString()
        });
    }
}
