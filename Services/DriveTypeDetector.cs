using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SDeleteGUI.Services;

internal enum DriveKind { Unknown, Ssd, Hdd, Flash, Network, CdRom, Ram }

internal static class DriveTypeDetector
{
	private const uint IoctlStorageQueryProperty = 0x002D1400;
	private const uint StorageDeviceSeekPenaltyProperty = 7;
	private const uint PropertyStandardQuery = 0;
	private const uint FileShareReadWrite = 0x3;
	private const uint OpenExisting = 3;

	[StructLayout(LayoutKind.Sequential)]
	private struct StoragePropertyQuery
	{
		public uint PropertyId;
		public uint QueryType;
		public byte AdditionalParameters;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct DeviceSeekPenaltyDescriptor
	{
		public uint Version;
		public uint Size;
		public byte IncursSeekPenalty;
	}

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern SafeFileHandle CreateFileW(
		string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
		uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool DeviceIoControl(
		SafeFileHandle hDevice, uint dwIoControlCode,
		ref StoragePropertyQuery lpInBuffer, uint nInBufferSize,
		out DeviceSeekPenaltyDescriptor lpOutBuffer, uint nOutBufferSize,
		out uint lpBytesReturned, IntPtr lpOverlapped);

	public static DriveKind Detect(DriveInfo drive)
	{
		try
		{
			switch (drive.DriveType)
			{
				case DriveType.Network: return DriveKind.Network;
				case DriveType.CDRom: return DriveKind.CdRom;
				case DriveType.Ram: return DriveKind.Ram;
				case DriveType.Removable: return DriveKind.Flash;

				case DriveType.Fixed:
					return QueryIncursSeekPenalty(drive.Name[0]) switch
					{
						true => DriveKind.Hdd,
						false => DriveKind.Ssd,
						null => DriveKind.Unknown
					};

				default:
					return DriveKind.Unknown;
			}
		}
		catch
		{
			return DriveKind.Unknown;
		}
	}

	public static string GetLabel(DriveKind kind) => kind switch
	{
		DriveKind.Ssd => Loc.Get("DriveTypeSsd"),
		DriveKind.Hdd => Loc.Get("DriveTypeHdd"),
		DriveKind.Flash => Loc.Get("DriveTypeFlash"),
		DriveKind.Network => Loc.Get("DriveTypeNetwork"),
		DriveKind.CdRom => Loc.Get("DriveTypeCdRom"),
		DriveKind.Ram => Loc.Get("DriveTypeRam"),
		_ => string.Empty
	};

	private static bool? QueryIncursSeekPenalty(char driveLetter)
	{
		using var handle = CreateFileW(
			$@"\\.\{driveLetter}:", 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

		if (handle.IsInvalid)
			return null;

		var query = new StoragePropertyQuery
		{
			PropertyId = StorageDeviceSeekPenaltyProperty,
			QueryType = PropertyStandardQuery
		};

		var ok = DeviceIoControl(
			handle, IoctlStorageQueryProperty,
			ref query, (uint)Marshal.SizeOf<StoragePropertyQuery>(),
			out var result, (uint)Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(),
			out _, IntPtr.Zero);

		return ok ? result.IncursSeekPenalty != 0 : null;
	}
}
