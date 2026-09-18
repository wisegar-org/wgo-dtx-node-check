using DtxNodeCheck.Core;

namespace DtxNodeCheck.Desktop.Shared;

public static class AppSettings
{
    public static DtxNodeRole? NodeRole { get; set; }
    public static bool InferNodeRole { get; set; }
    public static string ApplicationName { get; set; } = "DTX Node Check";
    public static string? ConfigFileName { get; set; }
}
