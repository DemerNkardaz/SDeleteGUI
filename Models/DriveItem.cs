using SDeleteGUI.Services;

namespace SDeleteGUI.Models;

/// <summary>
/// Model that represents a drive item in the list of available drives for cleaning free space.
/// It contains properties for the drive name, letter, display name, glyph, total size, and free space.
/// </summary>
public class DriveItem
{
	public string Name { get; set; } = string.Empty;
	public string DriveLetter { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Glyph { get; set; } = "\uEDA2";
	public long TotalSize { get; set; }
	public long FreeSpace { get; set; }
	public string TypeLabel { get; set; } = string.Empty;
	public bool IsFlashBased { get; set; }
	public bool IsRecommendedForClean { get; set; } = true;

	public double UsedPercent =>
		TotalSize > 0 ? (double)(TotalSize - FreeSpace) / TotalSize * 100 : 0;

	public string CapacityText =>
		$"{FormatBytes(TotalSize - FreeSpace)} / {FormatBytes(TotalSize)}";

	private static string FormatBytes(long bytes)
	{
		double gb = bytes / 1024d / 1024d / 1024d;
		double tb = gb / 1024d;
		return
			tb >= 1
			? $"{tb:0.#} {Services.Loc.Get("UnitTB")}"
			: gb >= 1
			? $"{gb:0.#} {Services.Loc.Get("UnitGB")}"
			: $"{bytes / 1024d / 1024d:0.#} {Services.Loc.Get("UnitMB")}";
	}
}
