using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace UI.Services;

public sealed class NotificationService : INotificationService
{
	private static readonly TimeSpan _defaultErrorExpiration = TimeSpan.FromSeconds(4);
	private static readonly TimeSpan _defaultWarningExpiration = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan _defaultInfoExpiration = TimeSpan.FromSeconds(3);

	private WindowNotificationManager? _manager;

	public void SetHostWindow(TopLevel topLevel)
	{
		_manager = new WindowNotificationManager(topLevel)
		{
			Position = NotificationPosition.BottomRight,
			MaxItems = 3
		};
	}

	public void ShowError(string message, TimeSpan? expiration = null)
	{
		Show("Error", message, NotificationType.Error, expiration ?? _defaultErrorExpiration);
	}

	public void ShowWarning(string message, TimeSpan? expiration = null)
	{
		Show("Warning", message, NotificationType.Warning, expiration ?? _defaultWarningExpiration);
	}

	public void ShowInfo(string message, TimeSpan? expiration = null)
	{
		Show("Info", message, NotificationType.Information, expiration ?? _defaultInfoExpiration);
	}

	public void ShowSuccess(string message, TimeSpan? expiration = null)
	{
		Show("Success", message, NotificationType.Success, expiration ?? _defaultInfoExpiration);
	}

	private void Show(string title, string message, NotificationType type, TimeSpan expiration)
	{
		_manager?.Show(new Notification(title, message, type, expiration));
	}
}
