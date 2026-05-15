using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI.Avalonia;

using SemiStep.Core.Recipes;
using SemiStep.UI.Coordinator;
using SemiStep.UI.Dialogs;
using SemiStep.UI.MainWindow;

using Serilog;

namespace SemiStep.UI;

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
			.UseHarfBuzz()
			.UseReactiveUI(_ => { })
			.LogToTrace();
	}

	public static void Run(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		BuildAvaloniaApp()
			.AfterSetup(_ =>
				// UseReactiveUI() above has already registered AvaloniaScheduler as
				// RxSchedulers.MainThreadScheduler. Initialize services here — after the
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
		var resetResult = workspace.Reset();
		if (resetResult.IsFailed)
		{
			Log.Warning("Workspace reset reported failures at startup: {Errors}",
				string.Join("; ", resetResult.Errors.Select(e => e.Message)));
		}

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
