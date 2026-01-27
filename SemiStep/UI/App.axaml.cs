using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI;

using Shared;

using UI.ViewModels;
using UI.Views;

namespace UI;

public class App : Application
{
	public static IServiceProvider? ServiceProvider { get; private set; }

	public static AppConfiguration? Configuration { get; private set; }

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

			if (Configuration is null)
			{
				throw new InvalidOperationException("Configuration not set. Call Run() with configuration before starting the app.");
			}

			var mainWindowViewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();
			mainWindowViewModel.Initialize(Configuration);

			desktop.MainWindow = new MainWindow
			{
				DataContext = mainWindowViewModel
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UseWin32()
			.UseSkia()
			.UseReactiveUI()
			.LogToTrace();
	}

	public static void Run(IServiceProvider serviceProvider, AppConfiguration configuration)
	{
		ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
	}
}
