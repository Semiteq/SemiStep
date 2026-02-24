namespace UI.Services;

public interface INotificationService
{
	void ShowError(string message, TimeSpan? expiration = null);

	void ShowWarning(string message, TimeSpan? expiration = null);

	void ShowInfo(string message, TimeSpan? expiration = null);

	void ShowSuccess(string message, TimeSpan? expiration = null);
}
