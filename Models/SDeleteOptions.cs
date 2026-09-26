using SDeleteGUI.Services;

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
		var fileName = Executable == SDeleteExecutable.Sdelete64 ? "sdelete64" : "sdelete";

		return string.IsNullOrWhiteSpace(SdeleteFolderPath)
			? fileName
			: Path.Combine(SdeleteFolderPath, fileName);
	}

	/// <summary>
	/// Collects all the command-line arguments based on the current options and returns them as a list of strings.
	/// Each argument is a separate string in the list, ready to be passed to Process.Start
	/// </summary>
	public List<string> BuildArguments()
	{
		var args = new List<string> { "-accepteula" };

		if (Passes > 0)
		{
			args.Add("-p");
			args.Add(Passes.ToString());
		}

		switch (Mode)
		{
			case SDeleteMode.DeleteFiles:
				if (Recursive) args.Add("-s");
				if (RemoveReadOnlyAttribute) args.Add("-r");

				foreach (var entry in TargetPaths)
				{
					var path = entry.Path;
					if (entry.IsDirectory && entry.ContentsOnly)
					{
						path = Path.Combine(path, "*.*");
					}
					args.Add(path);
				}
				break;

			case SDeleteMode.CleanFreeSpace:
				args.Add(CleanMode == CleanMode.ZeroFill ? "-z" : "-c");

				foreach (var drive in SelectedDrives)
				{
					var normalized = drive.Trim().TrimEnd('\\', '/', '"');
					args.Add(normalized);
				}
				break;
		}

		return args;
	}

	/// <summary>
	/// Validates the current options and returns a list of error messages if any required options are missing or invalid.
	/// If the list is empty, the options are valid.
	/// </summary>
	public List<string> Validate()
	{
		var errors = new List<string>();

		if (Passes < 1)
			errors.Add(Services.Loc.Get("ValidationPassesTooSmall"));

		switch (Mode)
		{
			case SDeleteMode.DeleteFiles:
				if (TargetPaths.Count == 0)
					errors.Add(Services.Loc.Get("ValidationNoPaths"));
				break;

			case SDeleteMode.CleanFreeSpace:
				if (SelectedDrives.Count == 0)
					errors.Add(Services.Loc.Get("ValidationNoDrives"));
				break;
		}

		return errors;
	}

	/// <summary>
	/// Collects the full command line to run SDelete
	/// with the current options, including the executable path and all arguments.
	/// </summary>
	public string BuildCommandLine()
	{
		var exe = QuoteIfNeeded(ResolveExecutablePath());
		var args = BuildArguments().Select(QuoteIfNeeded);

		return string.Join(' ', new[] { exe }.Concat(args));
	}

	private static string QuoteIfNeeded(string value) =>
		value.Contains(' ') ? $"\"{value}\"" : value;
}
