namespace SDeleteGUI.Models;

/// <summary>
/// Defines the mode of operation for SDeleteGUI, either deleting files or cleaning free space on drives.
/// </summary>
public enum SDeleteMode
{
    DeleteFiles,
    CleanFreeSpace
}

/// <summary>
/// Cleaning mode for the “Clean Free Space” operation,
/// determining how the free space on the selected drives will be cleaned.
/// </summary>
public enum CleanMode
{
    ZeroFill,   // -z
    CleanFree   // -c
}

/// <summary>
/// Selects which SDelete executable to use (32-bit or 64-bit).
/// </summary>
public enum SDeleteExecutable
{
    Sdelete,
    Sdelete64
}

/// <summary>
/// Model that represents the options for running SDeleteGUI.
/// It contains properties for common options, file deletion options, and drive cleaning options.
/// </summary>
public class SDeleteOptions
{
    // Common options
    public SDeleteExecutable Executable { get; set; } = SDeleteExecutable.Sdelete;
    public string? SdeleteFolderPath { get; set; }
    public int Passes { get; set; } = 3;

    // File deletion tab options
    public SDeleteMode Mode { get; set; } = SDeleteMode.DeleteFiles;
    public List<PathEntry> TargetPaths { get; set; } = new();
    public bool Recursive { get; set; }
    public bool RemoveReadOnlyAttribute { get; set; }
    public bool ContentsOnly { get; set; }

    // Drive cleaning tab options
    public List<string> SelectedDrives { get; set; } = new();
    public CleanMode CleanMode { get; set; } = CleanMode.CleanFree;

    /// <summary>
    /// Collects the path to the selected sdelete executable based on the Executable enum and SdeleteFolderPath.
	/// Returns the full path to the executable, or throws an exception if the path is invalid
    /// </summary>
    public string ResolveExecutablePath()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Collects all the command-line arguments based on the current options and returns them as a list of strings.
	/// Each argument is a separate string in the list, ready to be passed to Process.Start
    /// </summary>
    public List<string> BuildArguments()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates the current options and returns a list of error messages if any required options are missing or invalid.
	/// If the list is empty, the options are valid.
    /// </summary>
    public List<string> Validate()
    {
        throw new NotImplementedException();
    }
}
