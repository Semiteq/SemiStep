using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI;

using UI.Services;
using UI.ViewModels;
using UI.Views;

namespace UI;

public class App : Application
{
	public static IServiceProvider? ServiceProvider { get; private set; }

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			if (ServiceProvider is null)
			{
				throw new InvalidOperationException("ServiceProvider not set. Call Run() before starting the app.");
			}

			var mainWindowViewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();
			mainWindowViewModel.Initialize();

			var mainWindow = new MainWindow { DataContext = mainWindowViewModel };
			desktop.MainWindow = mainWindow;

			var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
			notificationService.SetHostWindow(mainWindow);
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UseWin32()
			.UseSkia()
			.UseReactiveUI()
			.LogToTrace();
	}

	public static void Run(IServiceProvider serviceProvider)
	{
		ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
		BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
	}
}
