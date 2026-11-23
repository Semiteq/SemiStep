using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Tools.PlcEmulator.Ui;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>Disposing stops the tick and the refresh before the server.</summary>
internal sealed class EmulatorHost : IDisposable
{
	private readonly EmulatedPlc _plc;
	private readonly RunEngine _engine;
	private readonly EmulatorLoop _loop;
	private readonly string _presetName;
	private readonly TimeProvider _timeProvider;
	private readonly CancellationTokenSource _tickCancellation = new();
	private MainWindowViewModel? _viewModel;
	private Task _tickTask = Task.CompletedTask;

	private EmulatorHost(EmulatedPlc plc, AppConfiguration configuration, string presetName, TimeProvider timeProvider)
	{
		_plc = plc;
		_engine = new RunEngine(plc, new CommittedRecipeLoader(plc, configuration));
		_loop = new EmulatorLoop(plc, _engine);
		_presetName = presetName;
		_timeProvider = timeProvider;
	}

	public static Result<EmulatorHost> Start(
		AppConfiguration configuration,
		EmulatorOptions options,
		string presetName,
		TimeProvider timeProvider)
	{
		var started = EmulatedPlc.Start(configuration, options);
		return started.IsFailed
			? started.ToResult<EmulatorHost>()
			: Result.Ok(new EmulatorHost(started.Value, configuration, presetName, timeProvider));
	}

	/// <summary>Call on the UI thread: the view model fills the row collections the window binds.</summary>
	public MainWindowViewModel CreateViewModel()
	{
		_viewModel = new MainWindowViewModel(_plc, _engine, _loop, _presetName, _timeProvider);
		return _viewModel;
	}

	public void StartTicking()
	{
		_tickTask = Task.Run(() => _loop.RunAsync(_timeProvider, _tickCancellation.Token));
	}

	public void Dispose()
	{
		_tickCancellation.Cancel();
		_viewModel?.Dispose();
		_tickTask.GetAwaiter().GetResult();
		_tickCancellation.Dispose();
		_plc.Dispose();
	}
}
