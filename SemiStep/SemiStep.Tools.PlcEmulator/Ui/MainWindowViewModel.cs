using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Threading;

using FluentResults;

using ReactiveUI;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed record StatusMessage(string Text, bool IsError);

internal sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
	private const string IdleGlyph = "■";
	private const string ActiveGlyph = "▶";

	private readonly EmulatedPlc _plc;
	private readonly RunEngine _engine;
	private readonly EmulatorLoop _loop;
	private readonly MemoryFieldCodec _codec;
	private readonly TimeProvider _timeProvider;
	private readonly CompositeDisposable _disposables = [];
	private readonly ObservableAsPropertyHelper<string> _stateGlyph;
	private readonly ObservableAsPropertyHelper<string> _subtitleText;
	private readonly ObservableAsPropertyHelper<string> _clientText;
	private readonly ObservableAsPropertyHelper<bool> _isClientConnected;
	private bool _isDisposed;

	public MainWindowViewModel(
		EmulatedPlc plc,
		RunEngine engine,
		EmulatorLoop loop,
		string presetName,
		TimeProvider timeProvider)
	{
		_plc = plc;
		_engine = engine;
		_loop = loop;
		_timeProvider = timeProvider;
		_codec = new MemoryFieldCodec(plc.Layout, plc.StringMaxChars);
		ConfigText = presetName;
		Endpoint = plc.Endpoint;
		Title = $"PLC emulator - {presetName} - {plc.Endpoint}";
		Blocks = [.. BlockSchema.For(plc.Blocks, plc.Layout, plc.StringMaxChars).Select(NewBlock)];
		SelectedBlock = Blocks.Single(block => block.Schema.Info.DbNumber == plc.Layout.ExecutionDb.DbNumber);
		State = loop.State;

		_stateGlyph = this.WhenAnyValue(x => x.State)
			.Select(state => state.IsActive ? ActiveGlyph : IdleGlyph)
			.ToProperty(this, x => x.StateGlyph);
		_subtitleText = this.WhenAnyValue(x => x.State)
			.Select(state => state.IsActive
				? "Values are read-only while a recipe runs."
				: "Double-click a value to change it. Enter writes it to the PLC memory.")
			.ToProperty(this, x => x.SubtitleText);
		_isClientConnected = this.WhenAnyValue(x => x.ClientCount)
			.Select(count => count > 0)
			.ToProperty(this, x => x.IsClientConnected);
		_clientText = this.WhenAnyValue(x => x.IsClientConnected)
			.Select(isConnected => isConnected ? "SemiStep connected" : "No client")
			.ToProperty(this, x => x.ClientText);

		Start = ReactiveCommand.Create(StartRun, WhenPhaseIs(RunPhase.Idle));
		Stop = ReactiveCommand.Create(StopRun, WhenPhaseIs(RunPhase.Running, RunPhase.Paused));
		Continue = ReactiveCommand.Create(ContinueRun, WhenPhaseIs(RunPhase.Paused));
		Observable.Merge(Start.ThrownExceptions, Stop.ThrownExceptions, Continue.ThrownExceptions)
			.Subscribe(ReportFailure)
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.SelectedBlock)
			.Subscribe(_ => RefreshSelectedBlock())
			.DisposeWith(_disposables);

		_loop.EventRaised += OnEventRaised;
		_disposables.Add(Disposable.Create(() => _loop.EventRaised -= OnEventRaised));
		var tickInterval = RunEngine.TickInterval;
		_disposables.Add(_timeProvider.CreateTimer(_ => OnUiThread(Refresh), null, tickInterval, tickInterval));
	}

	public string Title { get; }

	public string ConfigText { get; }

	public string Endpoint { get; }

	public IReadOnlyList<DataBlockViewModel> Blocks { get; }

	public DataBlockViewModel SelectedBlock
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public RunState State
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public StatusMessage Message
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = new(string.Empty, false);

	public int ClientCount
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public string StateGlyph => _stateGlyph.Value;

	public string SubtitleText => _subtitleText.Value;

	public string ClientText => _clientText.Value;

	public bool IsClientConnected => _isClientConnected.Value;

	public ReactiveCommand<Unit, Unit> Start { get; }

	public ReactiveCommand<Unit, Unit> Stop { get; }

	public ReactiveCommand<Unit, Unit> Continue { get; }

	public void Edit(MemoryRowViewModel row, string text)
	{
		var field = row.Field;
		var target = $"DB{field.DbNumber}.{field.Name}";
		if (State.IsActive)
		{
			Show($"{target}: values are read-only while a recipe runs.", true);
			return;
		}

		var encoded = _codec.Encode(field, text);
		if (encoded.IsFailed)
		{
			Show($"{target}: {encoded.Errors[0].Message}.", true);
			return;
		}

		_plc.Write(field.DbNumber, field.Offset, encoded.Value);
		var warning = ApplySelectedBlock();
		Show(warning ?? $"{target} set to {_codec.FormatValue(field.Type, encoded.Value)}", warning is not null);
	}

	public void Dispose()
	{
		_isDisposed = true;
		_disposables.Dispose();
	}

	private static void OnUiThread(Action action)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			action();
		}
		else
		{
			Dispatcher.UIThread.Post(action);
		}
	}

	private DataBlockViewModel NewBlock(BlockSchema schema)
	{
		return new DataBlockViewModel(schema, _codec);
	}

	private IObservable<bool> WhenPhaseIs(params RunPhase[] phases)
	{
		return this.WhenAnyValue(x => x.State, state => phases.Contains(state.Phase));
	}

	private void StartRun()
	{
		Report(_engine.Run(), "Recipe started");
	}

	private void StopRun()
	{
		Report(_engine.Stop(), "Recipe stopped");
	}

	private void ContinueRun()
	{
		Report(_engine.Continue(), "Continued after the pause");
	}

	private void Report(Result result, string successText)
	{
		if (result.IsFailed)
		{
			Show(string.Join(' ', result.Errors.Select(error => error.Message)), true);
		}
		else
		{
			Show(successText, false);
		}

		State = _loop.State;
	}

	private void ReportFailure(Exception exception)
	{
		Show(exception.Message, true);
		State = _loop.State;
	}

	private void OnEventRaised(EmulatorEvent emulatorEvent)
	{
		OnUiThread(() => Apply(emulatorEvent));
	}

	private void Apply(EmulatorEvent emulatorEvent)
	{
		switch (emulatorEvent)
		{
			case RecipeCommitted committed:
				Show($"SemiStep committed a recipe: {committed.Lines} lines", false);
				break;
			case RequestRefused refused:
				Show(refused.Text, true);
				break;
			case ClientConnected:
				ClientCount++;
				break;
			case ClientDisconnected:
				ClientCount = Math.Max(0, ClientCount - 1);
				break;
			case TickFailed failed:
				Show($"The emulator tick stopped: {failed.Text}", true);
				break;
		}
	}

	private void Refresh()
	{
		if (_isDisposed)
		{
			return;
		}

		var previous = State;
		State = _loop.State;
		var finishedNaturally = previous.ActualLine >= previous.LineCount;
		if (previous.Phase == RunPhase.Finishing && State.Phase == RunPhase.Idle && finishedNaturally)
		{
			Show("Recipe finished", false);
		}

		RefreshSelectedBlock();
	}

	private void RefreshSelectedBlock()
	{
		if (ApplySelectedBlock() is { } warning)
		{
			Show(warning, true);
		}
	}

	private string? ApplySelectedBlock()
	{
		var info = SelectedBlock.Schema.Info;
		return SelectedBlock.Apply(_plc.Read(info.DbNumber, 0, info.Size), _timeProvider.GetUtcNow());
	}

	private void Show(string text, bool isError)
	{
		Message = new StatusMessage(text, isError);
	}
}
