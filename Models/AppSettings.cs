namespace SDeleteGUI.Models;

/// <summary>
/// Serialized user settings model
/// Stored at %LOCALAPPDATA%\Nkardaz\SDeleteGUI\settings.json.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Version for migration purposes.
    /// </summary>
    public int Version { get; set; } = 1;

    // Common options
    public string Executable { get; set; } = "Sdelete";
    public string? SdeleteFolderPath { get; set; }
    public int Passes { get; set; } = 3;

    public string Mode { get; set; } = "DeleteFiles";

    public bool Recursive { get; set; }
    public bool RemoveReadOnlyAttribute { get; set; }

    public string CleanMode { get; set; } = "CleanFree";
}
