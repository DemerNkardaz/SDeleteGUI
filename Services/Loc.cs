using Microsoft.Windows.ApplicationModel.Resources;

namespace SDeleteGUI.Services;

/// <summary>
/// ResourceLoader Wrapper for accessing localized strings from C# code.
/// Creates ResourceLoader once and holds it statically
/// to avoid creating a new object every time.
/// Usage:
///  Loc.Get("LocalDisk")
///  Loc.Format("RunCommandCompleted", exitCode)
/// </summary>
internal static class Loc
{
	private static readonly ResourceLoader Loader = new();

	public static string Get(string key)
	{
		try
		{
			return Loader.GetString(key);
		}
		catch
		{
			return key;
		}
	}

	public static string Format(string key, params object[] args)
	{
		var template = Get(key);
		try
		{
			return string.Format(template, args);
		}
		catch
		{
			return template;
		}
	}
}
