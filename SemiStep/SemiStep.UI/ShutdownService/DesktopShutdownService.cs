using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace SemiStep.UI.ShutdownService;

internal class DesktopShutdownService
{
	public static void Shutdown()
	{
		if (Application.Current?.ApplicationLifetime
			is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			lifetime.Shutdown();
		}
	}
}
