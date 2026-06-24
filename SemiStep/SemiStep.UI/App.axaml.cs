using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI.Avalonia;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.UI.Coordinator;
using SemiStep.UI.Dialogs;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.Styles;

using Serilog;

namespace SemiStep.UI;

public class App : Application
{
	private static bool _started;

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

				var gridStyle = _serviceProvider.GetRequiredService<GridStyleOptions>();
				// Both installers must run before MainWindow construction. {DynamicResource}
				// lookups against Application.Resources resolve lazily at first realize;
				// missing keys at that point render as default/nothing.
				ExecutionPaletteInstaller.Install(Resources, gridStyle);
				CellPaletteInstaller.Install(Resources, gridStyle);

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
		EnsureSingleStart();
		BuildAvaloniaApp()
			.AfterSetup(_ =>
				// Initialize after setup: UseReactiveUI() has registered AvaloniaScheduler as
				// MainThreadScheduler by now, so ReactiveCommand singletons capture it at construction.
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
		var session = provider.GetRequiredService<RecipeSession>();

		// Empty-recipe analysis can surface validator warnings; log them but never block startup.
		var resetResult = session.Reset();
		if (resetResult.IsFailed)
		{
			Log.Warning("Session reset reported failures at startup: {Errors}",
				resetResult.FormatErrors());
		}

		var coordinator = provider.GetRequiredService<RecipeCoordinator>();
		coordinator.Initialize();

		var gridViewModel = provider.GetRequiredService<RecipeGridViewModel>();
		coordinator.Mutated += gridViewModel.OnMutation;
	}

	public static void RunErrorWindow(IReadOnlyList<string> errors)
	{
		EnsureSingleStart();
		BuildAvaloniaApp()
			.AfterSetup(builder =>
			{
				var app = (App)builder.Instance!;
				app._startupErrors = errors;
			})
			.StartWithClassicDesktopLifetime([]);
	}

	private static void EnsureSingleStart()
	{
		if (_started)
		{
			throw new InvalidOperationException(
				"App has already been started. Run() and RunErrorWindow() must be called at most once per process.");
		}

		_started = true;
	}
}
