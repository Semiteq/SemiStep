using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;

using SemiStep.UI.Coordinator;
using SemiStep.UI.Localization;

namespace SemiStep.UI.Plc;

public sealed class PlcMonitorViewModel : ReactiveObject, IDisposable
{
	private const string MissingSnapshotText = "\u2014";

	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = [];
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry;
	private readonly IScheduler _scheduler;
	private PlcExecutionInfo _lastInfo = PlcExecutionInfo.Empty;
	private bool _isLinkConnected;
	private DateTime _baseUtc;

	public PlcMonitorViewModel(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		IScheduler scheduler)
	{
		_coordinator = coordinator;
		_recipeMetadataRegistry = recipeMetadataRegistry;
		_scheduler = scheduler;
		_baseUtc = _scheduler.Now.UtcDateTime;
		_isLinkConnected = coordinator.IsConnected;

		coordinator.ExecutionState
			.Subscribe(OnExecutionStateChanged)
			.DisposeWith(_disposables);

		coordinator.PlcStateChanged
			.Subscribe(OnPlcStateChanged)
			.DisposeWith(_disposables);

		coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => coordinator.Mutated -= OnCoordinatorMutated));

		Observable.Interval(TimeSpan.FromSeconds(1), _scheduler)
			.Subscribe(_ => OnInterpolationTick())
			.DisposeWith(_disposables);

		RecalculateAll();
	}

	public bool IsRecipeActive
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public int ActualLine
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public string TimeLeftInStepCaption
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = Resources.StatusStepLabel;

	public string TimeLeftInRecipeCaption
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = Resources.StatusRecipeLabel;

	public string TimeLeftInStepText
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = MissingSnapshotText;

	public string TimeLeftInRecipeText
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = MissingSnapshotText;

	public string StateStepText
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = Resources.ExecutionIdle;

	public string LoopText
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = string.Empty;

	public string? ExecutionToolTip
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private static string FormatTimeSpan(TimeSpan value)
	{
		return value.TotalHours >= 24
			? value.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
			: value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
	}

	private void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		var clamped = ApplyMonotonicClamp(info);

		_lastInfo = clamped;
		_baseUtc = _scheduler.Now.UtcDateTime;
		ActualLine = clamped.ActualLine;

		RecalculateAll();
	}

	private void OnPlcStateChanged(PlcSessionSnapshot snapshot)
	{
		_isLinkConnected = snapshot.ConnectionState == PlcConnectionState.Connected;

		RecalculateAll();
	}

	// Without a link the PLC's execution state is unknown, so the bar claims neither a run nor idle.
	private void RecalculateAll()
	{
		IsRecipeActive = _isLinkConnected && _lastInfo.RecipeActive;

		RecalculateTexts(_lastInfo);
		RecalculateExecutionTexts();
	}

	private PlcExecutionInfo ApplyMonotonicClamp(PlcExecutionInfo incoming)
	{
		return !_lastInfo.RecipeActive || !incoming.RecipeActive
			? incoming
			: incoming.ActualLine == _lastInfo.ActualLine
			&& incoming.StepCurrentTime < _lastInfo.StepCurrentTime
			? (incoming with { StepCurrentTime = _lastInfo.StepCurrentTime })
			: incoming;
	}

	private void OnInterpolationTick()
	{
		if (!IsRecipeActive)
		{
			return;
		}

		var delta = (_scheduler.Now.UtcDateTime - _baseUtc).TotalSeconds;
		var interpolatedElapsed = _lastInfo.StepCurrentTime + delta;
		var interpolated = _lastInfo with { StepCurrentTime = (float)interpolatedElapsed };

		RecalculateTexts(interpolated);
	}

	private void OnCoordinatorMutated(MutationSignal _)
	{
		RecalculateTexts(_lastInfo);
		RecalculateExecutionTexts();
	}

	private void RecalculateExecutionTexts()
	{
		if (!IsRecipeActive)
		{
			StateStepText = _isLinkConnected ? Resources.ExecutionIdle : Resources.ExecutionNoData;
			LoopText = string.Empty;
			ExecutionToolTip = null;
			return;
		}

		var snapshot = _coordinator.Snapshot;
		StateStepText = FormatRunningStep(_lastInfo.ActualLine, snapshot.Recipe.StepCount);

		var loops = ActiveLoops.Resolve(snapshot, _lastInfo);
		LoopText = string.Join(" \u203A ", loops.Select((loop, position) => FormatLoop(
			position == 0 ? Resources.LoopPassFirstFormat : Resources.LoopPassNextFormat,
			loop)));
		var levels = loops.Select((loop, position) => string.Format(
			CultureInfo.InvariantCulture,
			position == 0 ? Resources.LoopLevelFirstFormat : Resources.LoopLevelNextFormat,
			position + 1,
			loop.Pass,
			loop.Loop.Iterations));
		ExecutionToolTip = string.Join(Environment.NewLine, levels.Prepend(Resources.ExecutionLockToolTip));
	}

	/// <summary>
	/// The line is 0-based; before the first step the PLC reports -1, and it holds the line one past the last step
	/// while it finishes. A line further on belongs to a longer PLC recipe and shows no total.
	/// </summary>
	private static string FormatRunningStep(int actualLine, int stepCount)
	{
		var isFinishHold = actualLine == stepCount && stepCount > 0;
		var step = isFinishHold ? stepCount : actualLine + 1;

		return actualLine < 0
			? Resources.ExecutionRunning
			: step > stepCount
			? string.Format(CultureInfo.InvariantCulture, Resources.ExecutionRunningStepOnlyFormat, step)
			: string.Format(
				CultureInfo.InvariantCulture,
				Resources.ExecutionRunningFormat,
				PadToDigitsOf(step, stepCount),
				stepCount);
	}

	private static string FormatLoop(string format, ActiveLoop loop)
	{
		return string.Format(
			CultureInfo.InvariantCulture,
			format,
			PadToDigitsOf(loop.Pass, loop.Loop.Iterations),
			loop.Loop.Iterations);
	}

	// U+2007 is one digit wide, so a counter keeps its width as it grows.
	private static string PadToDigitsOf(int value, int total)
	{
		var digits = total.ToString(CultureInfo.InvariantCulture).Length;

		return value.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '\u2007');
	}

	private void RecalculateTexts(PlcExecutionInfo info)
	{
		var snapshot = _coordinator.Snapshot;

		if (snapshot.Recipe.Steps.Count == 0 || !_isLinkConnected)
		{
			TimeLeftInStepCaption = Resources.StatusStepLabel;
			TimeLeftInStepText = MissingSnapshotText;
			TimeLeftInRecipeCaption = Resources.StatusRecipeLabel;
			TimeLeftInRecipeText = MissingSnapshotText;
			return;
		}

		if (!IsRecipeActive)
		{
			TimeLeftInStepCaption = Resources.StatusStepLabel;
			TimeLeftInStepText = MissingSnapshotText;
			TimeLeftInRecipeCaption = Resources.RecipeTimerIdleCaption;
			TimeLeftInRecipeText = FormatTimeSpan(snapshot.TotalDuration);
			return;
		}

		var stepLeft = ExecutionTimeEstimator.TimeLeftInStep(snapshot, info, _recipeMetadataRegistry);
		var recipeLeft = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, info);

		TimeLeftInStepCaption = Resources.StepTimerRunningCaption;
		TimeLeftInStepText = FormatTimeSpan(stepLeft);
		TimeLeftInRecipeCaption = Resources.RecipeTimerRunningCaption;
		TimeLeftInRecipeText = FormatTimeSpan(recipeLeft);
	}
}
