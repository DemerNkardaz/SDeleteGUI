using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SDeleteGUI.Services;

internal static class SystemPathGuard
{
	private static readonly string[] ProgramFilesSystemFolders =
	{
		"WindowsApps",
		"ModifiableWindowsApps",
		"Windows Defender",
		"Windows Defender Advanced Threat Protection",
		"Windows NT",
		"WindowsPowerShell",
		"Windows Mail",
		"Windows Media Player",
		"Windows Photo Viewer",
		"Windows Portable Devices",
		"Internet Explorer",
		"Microsoft Update Health Tools",
		Path.Combine("Common Files", "Microsoft Shared"),
		Path.Combine("Common Files", "System"),
	};

	private static readonly Lazy<RuleSet> Rules = new(BuildRules);

	public static bool IsProtected(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return true;

		try
		{
			var lexical = Normalize(path);

			if (lexical.IndexOf(':', 2) >= 0)
				return true;

			if (MatchesRules(lexical))
				return true;

			var resolved = TryResolveFinalPath(lexical);
			return resolved is not null && MatchesRules(Normalize(resolved));
		}
		catch
		{
			return true;
		}
	}

	private static bool MatchesRules(string path)
	{
		var rules = Rules.Value;

		if (path.Equals(rules.SystemRoot, StringComparison.OrdinalIgnoreCase))
			return true;

		foreach (var allowed in rules.Allowed)
		{
			if (IsSameOrDescendant(path, allowed))
				return false;
		}

		foreach (var tree in rules.Tree)
		{
			if (IsSameOrDescendant(path, tree) || IsStrictDescendant(tree, path))
				return true;
		}

		foreach (var exact in rules.Exact)
		{
			if (path.Equals(exact, StringComparison.OrdinalIgnoreCase) || IsStrictDescendant(exact, path))
				return true;
		}

		foreach (var (parent, prefix) in rules.Prefix)
		{
			if (IsSameOrDescendant(parent, path))
				return true;

			if (IsStrictDescendant(path, parent) &&
				FirstSegmentAfter(parent, path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static RuleSet BuildRules()
	{
		var rules = new RuleSet();

		var windowsRaw = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (string.IsNullOrWhiteSpace(windowsRaw))
			windowsRaw = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

		var windows = Normalize(windowsRaw);
		var systemRoot = Path.GetPathRoot(windows) ?? @"C:\";
		rules.SystemRoot = Normalize(systemRoot);

		rules.AddTree(windows);
		rules.AddAllowed(Path.Combine(windows, "Temp"));

		foreach (var name in new[] { "Boot", "EFI", "Recovery", "System Volume Information" })
			rules.AddTree(Path.Combine(systemRoot, name));

		foreach (var name in new[] { "bootmgr", "BOOTNXT", "BOOTSECT.BAK", "pagefile.sys", "hiberfil.sys", "swapfile.sys" })
			rules.AddTree(Path.Combine(systemRoot, name));

		rules.AddExact(Path.Combine(systemRoot, "$Recycle.Bin"));

		var programFiles = new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" }
			.Select(name => Environment.GetEnvironmentVariable(name))
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => Normalize(p!))
			.Distinct(StringComparer.OrdinalIgnoreCase);

		foreach (var pf in programFiles)
		{
			foreach (var name in ProgramFilesSystemFolders)
				rules.AddTree(Path.Combine(pf, name));
		}

		var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
		if (!string.IsNullOrWhiteSpace(programData))
		{
			foreach (var name in new[] { "Microsoft", "USOPrivate", "USOShared" })
				rules.AddTree(Path.Combine(programData, name));
		}

		var usersRoot = Path.Combine(systemRoot, "Users");
		rules.AddTree(Path.Combine(usersRoot, "Default"));

		var profiles = new List<string>();
		var current = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrWhiteSpace(current))
			profiles.Add(current);

		var notProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{ "Public", "All Users", "Default", "Default User" };

		try
		{
			foreach (var dir in Directory.EnumerateDirectories(usersRoot))
			{
				if (notProfiles.Contains(Path.GetFileName(dir)))
					continue;

				if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
					continue;

				profiles.Add(dir);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}

		foreach (var profile in profiles.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase))
			AddProfileRules(rules, profile);

		return rules;
	}

	private static void AddProfileRules(RuleSet rules, string profile)
	{
		var local = Path.Combine(profile, "AppData", "Local");
		var roaming = Path.Combine(profile, "AppData", "Roaming");

		rules.AddPrefix(profile, "ntuser.");
		rules.AddPrefix(Path.Combine(local, "Microsoft", "Windows"), "UsrClass.dat");

		foreach (var name in new[] { "Protect", "Crypto", "SystemCertificates", "Credentials", "Vault" })
			rules.AddTree(Path.Combine(roaming, "Microsoft", name));

		foreach (var name in new[] { "Credentials", "Vault" })
			rules.AddTree(Path.Combine(local, "Microsoft", name));

		foreach (var prefix in new[] { "Microsoft.Windows.", "MicrosoftWindows.", "windows." })
			rules.AddPrefix(Path.Combine(local, "Packages"), prefix);
	}


	private static string Normalize(string path)
	{
		var full = Path.GetFullPath(path);
		var root = Path.GetPathRoot(full) ?? string.Empty;
		return full.Length > root.Length ? full.TrimEnd('\\', '/') : full;
	}

	private static bool IsSameOrDescendant(string path, string root) =>
		path.Equals(root, StringComparison.OrdinalIgnoreCase) || IsStrictDescendant(path, root);

	private static bool IsStrictDescendant(string path, string root)
	{
		var prefix = root.EndsWith('\\') ? root : root + "\\";
		return path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
	}

	private static string FirstSegmentAfter(string parent, string path)
	{
		var rest = path[(parent.TrimEnd('\\').Length + 1)..];
		var end = rest.IndexOf('\\');
		return end < 0 ? rest : rest[..end];
	}

	private sealed class RuleSet
	{
		public string SystemRoot = string.Empty;
		public readonly List<string> Tree = new();
		public readonly List<string> Exact = new();
		public readonly List<string> Allowed = new();
		public readonly List<(string Parent, string Prefix)> Prefix = new();

		public void AddTree(string path) => Tree.Add(Normalize(path));
		public void AddExact(string path) => Exact.Add(Normalize(path));
		public void AddAllowed(string path) => Allowed.Add(Normalize(path));
		public void AddPrefix(string parent, string prefix) => Prefix.Add((Normalize(parent), prefix));
	}


	private const uint FileShareAll = 0x7;
	private const uint OpenExisting = 3;
	private const uint FileFlagBackupSemantics = 0x02000000;

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern SafeFileHandle CreateFileW(
		string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
		uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern uint GetFinalPathNameByHandleW(
		SafeFileHandle hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

	private static string? TryResolveFinalPath(string path)
	{
		using var handle = CreateFileW(path, 0, FileShareAll, IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);
		if (handle.IsInvalid)
			return null;

		var sb = new StringBuilder(1024);
		var length = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, 0);

		if (length > sb.Capacity)
		{
			sb = new StringBuilder((int)length);
			length = GetFinalPathNameByHandleW(handle, sb, (uint)sb.Capacity, 0);
		}

		if (length == 0 || length > sb.Capacity)
			return null;

		var result = sb.ToString();

		if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
			return @"\\" + result[8..];

		return result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..] : result;
	}
}
