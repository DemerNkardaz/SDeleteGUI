using System.Runtime.InteropServices;

namespace SDeleteGUI.Services;

internal static class SleepInhibitor
{
	[Flags]
	private enum ExecutionState : uint
	{
		Continuous = 0x80000000,
		SystemRequired = 0x00000001
	}

	[DllImport("kernel32.dll")]
	private static extern uint SetThreadExecutionState(ExecutionState state);

	public static void Prevent() =>
		SetThreadExecutionState(ExecutionState.Continuous | ExecutionState.SystemRequired);

	public static void Allow() =>
		SetThreadExecutionState(ExecutionState.Continuous);
}
