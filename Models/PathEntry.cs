namespace SDeleteGUI.Models;

/// <summary>
/// Model that represents a path entry in the list of target paths for deletion or cleaning.
/// It contains the path string, a flag indicating if it's a directory,
/// and a flag for whether to delete only the contents of the directory.
/// </summary>
public class PathEntry
{
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool ContentsOnly { get; set; }
}
