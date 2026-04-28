using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Domain;

using Microsoft.Extensions.DependencyInjection;

using UI.Coordinator;
using UI.Dialogs;
using UI.MainWindow;

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

				var mainWindow = new MainWindow.MainWindow { DataContext = mainWindowViewModel };
				desktop.MainWindow = mainWindow;
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
		BuildAvaloniaApp()
			.AfterSetup(_ =>
				// UseReactiveUI() above has already registered AvaloniaScheduler as
				// RxApp.MainThreadScheduler. Initialize services here — after the
				// scheduler is set — so that ReactiveCommand singletons capture the
				// correct scheduler at construction time.
				InitializeServices(serviceProvider))
			.AfterSetup(builder =>
			{
				var app = (App)builder.Instance!;
				app._serviceProvider = serviceProvider;
			})
			.StartWithClassicDesktopLifetime([]);
	}

	private static void InitializeServices(IServiceProvider provider)
	{
		var workspace = provider.GetRequiredService<RecipeWorkspace>();
		workspace.Reset();

		var coordinator = provider.GetRequiredService<RecipeMutationCoordinator>();
		coordinator.Initialize();
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
