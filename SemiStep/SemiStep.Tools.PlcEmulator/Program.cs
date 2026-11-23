using Avalonia;
using Avalonia.Controls;

using FluentResults;

using ReactiveUI.Avalonia;

using SemiStep.Tools.PlcEmulator.Ui;

namespace SemiStep.Tools.PlcEmulator;

internal static class Program
{
	private const int FailureExitCode = 1;

	[STAThread]
	public static int Main(string[] arguments)
	{
		var started = Start(arguments);
		if (started.IsFailed)
		{
			IReadOnlyList<string> errors = [.. started.Errors.Select(error => error.Message)];
			RunWindow(() => new ErrorWindow(errors));
			return FailureExitCode;
		}

		using var host = started.Value;
		RunWindow(() => OpenMainWindow(host));
		return 0;
	}

	private static Result<EmulatorHost> Start(string[] arguments)
	{
		try
		{
			return Task.Run(() => EmulatorStartup.StartAsync(arguments, TimeProvider.System)).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			return Result.Fail<EmulatorHost>(ex.Message);
		}
	}

	private static MainWindow OpenMainWindow(EmulatorHost host)
	{
		var window = new MainWindow { DataContext = host.CreateViewModel() };
		host.StartTicking();
		return window;
	}

	private static void RunWindow(Func<Window> createMainWindow)
	{
		AppBuilder.Configure(() => new App(createMainWindow))
			.UseWin32()
			.UseSkia()
			.UseHarfBuzz()
			.UseReactiveUI(_ => { })
			.StartWithClassicDesktopLifetime([]);
	}
}
