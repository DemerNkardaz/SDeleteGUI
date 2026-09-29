using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace SDeleteGUI.Services;

internal static class AppNotifier
{
	private static bool _registered;

	public static void EnsureRegistered()
	{
		if (_registered)
			return;

		try
		{
			AppNotificationManager.Default.Register();
			_registered = true;
		}
		catch
		{
		}
	}

	public static void NotifyCompleted(string title, string message)
	{
		if (!_registered)
			return;

		try
		{
			var builder = new AppNotificationBuilder()
				.AddText(title)
				.AddText(message);

			AppNotificationManager.Default.Show(builder.BuildNotification());
		}
		catch
		{
		}
	}
}
