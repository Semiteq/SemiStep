using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI;

using UI.Services;
using UI.ViewModels;
using UI.Views;
using UI.Windows;

namespace UI;

public class App : Application
{
	private IServiceProvider? _serviceProvider;
	private IReadOnlyList<string>? _startupErrors;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			if (_startupErrors is not null)
			{
				desktop.MainWindow = new ErrorWindow(_startupErrors);
			}
			else
			{
				if (_serviceProvider is null)
				{
					throw new InvalidOperationException("ServiceProvider not set. Call Run() before starting the app.");
				}

				var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
				mainWindowViewModel.Initialize();

				var mainWindow = new MainWindow { DataContext = mainWindowViewModel };
				desktop.MainWindow = mainWindow;

				var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
				notificationService.SetHostWindow(mainWindow);
			}
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
		ArgumentNullException.ThrowIfNull(serviceProvider);
		RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
		BuildAvaloniaApp()
			.AfterSetup(builder =>
			{
				var app = (App)builder.Instance!;
				app._serviceProvider = serviceProvider;
			})
			.StartWithClassicDesktopLifetime([]);
	}

	public static void RunErrorWindow(IReadOnlyList<string> errors)
	{
		BuildAvaloniaApp()
			.AfterSetup(builder =>
			{
				var app = (App)builder.Instance!;
				app._startupErrors = errors;
			})
			.StartWithClassicDesktopLifetime([]);
	}
}
