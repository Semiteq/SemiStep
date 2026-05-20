using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.Plc;

public sealed class PlcMonitorViewModel : ReactiveObject, IDisposable
{
	private const string IdleStepText = "00:00:00";
	private const string MissingSnapshotText = "—";

	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry;
	private readonly IScheduler _scheduler;

	private int _actualLine;
	private int _forLoopCount1;
	private int _forLoopCount2;
	private int _forLoopCount3;
	private bool _isRecipeActive;
	private PlcExecutionInfo _lastInfo = PlcExecutionInfo.Empty;
	private DateTime _baseUtc;
	private string _timeLeftInRecipeText = MissingSnapshotText;
	private string _timeLeftInStepText = MissingSnapshotText;

	public PlcMonitorViewModel(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		IScheduler scheduler)
	{
		_coordinator = coordinator;
		_recipeMetadataRegistry = recipeMetadataRegistry;
		_scheduler = scheduler;
		_baseUtc = _scheduler.Now.UtcDateTime;

		coordinator.ExecutionState
			.Subscribe(OnExecutionStateChanged)
			.DisposeWith(_disposables);

		coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => coordinator.Mutated -= OnCoordinatorMutated));

		Observable.Interval(TimeSpan.FromSeconds(1), _scheduler)
			.Subscribe(_ => OnInterpolationTick())
			.DisposeWith(_disposables);

		RecalculateTexts(_lastInfo);
	}

	public bool IsRecipeActive
	{
		get => _isRecipeActive;
		private set => this.RaiseAndSetIfChanged(ref _isRecipeActive, value);
	}

	public int ActualLine
	{
		get => _actualLine;
		private set => this.RaiseAndSetIfChanged(ref _actualLine, value);
	}

	public string TimeLeftInStepText
	{
		get => _timeLeftInStepText;
		private set => this.RaiseAndSetIfChanged(ref _timeLeftInStepText, value);
	}

	public string TimeLeftInRecipeText
	{
		get => _timeLeftInRecipeText;
		private set => this.RaiseAndSetIfChanged(ref _timeLeftInRecipeText, value);
	}

	public int ForLoopCount1
	{
		get => _forLoopCount1;
		private set => this.RaiseAndSetIfChanged(ref _forLoopCount1, value);
	}

	public int ForLoopCount2
	{
		get => _forLoopCount2;
		private set => this.RaiseAndSetIfChanged(ref _forLoopCount2, value);
	}

	public int ForLoopCount3
	{
		get => _forLoopCount3;
		private set => this.RaiseAndSetIfChanged(ref _forLoopCount3, value);
	}

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private static string FormatTimeSpan(TimeSpan value)
	{
		return value.TotalHours >= 24
			? value.ToString(@"d\.hh\:mm\:ss")
			: value.ToString(@"hh\:mm\:ss");
	}

	private void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		var clamped = ApplyMonotonicClamp(info);

		_lastInfo = clamped;
		_baseUtc = _scheduler.Now.UtcDateTime;

		IsRecipeActive = clamped.RecipeActive;
		ActualLine = clamped.ActualLine;
		ForLoopCount1 = clamped.ForLoopCount1;
		ForLoopCount2 = clamped.ForLoopCount2;
		ForLoopCount3 = clamped.ForLoopCount3;

		RecalculateTexts(_lastInfo);
	}

	private PlcExecutionInfo ApplyMonotonicClamp(PlcExecutionInfo incoming)
	{
		if (!_lastInfo.RecipeActive || !incoming.RecipeActive)
		{
			return incoming;
		}

		if (incoming.ActualLine == _lastInfo.ActualLine
			&& incoming.StepCurrentTime < _lastInfo.StepCurrentTime)
		{
			return incoming with { StepCurrentTime = _lastInfo.StepCurrentTime };
		}

		return incoming;
	}

	private void OnInterpolationTick()
	{
		if (!_lastInfo.RecipeActive)
		{
			return;
		}

		var delta = (_scheduler.Now.UtcDateTime - _baseUtc).TotalSeconds;
		var interpolatedElapsed = _lastInfo.StepCurrentTime + delta;
		var interpolated = _lastInfo with { StepCurrentTime = (float)interpolatedElapsed };

		RecalculateTexts(interpolated);
	}

	private void OnCoordinatorMutated(MutationSignal signal)
	{
		_ = signal;
		RecalculateTexts(_lastInfo);
	}

	private void RecalculateTexts(PlcExecutionInfo info)
	{
		var snapshot = _coordinator.Snapshot;

		if (snapshot.Recipe.Steps.Count == 0)
		{
			TimeLeftInStepText = MissingSnapshotText;
			TimeLeftInRecipeText = MissingSnapshotText;
			return;
		}

		if (!info.RecipeActive)
		{
			TimeLeftInStepText = IdleStepText;
			TimeLeftInRecipeText = FormatTimeSpan(snapshot.TotalDuration);
			return;
		}

		var stepLeft = ExecutionTimeEstimator.TimeLeftInStep(snapshot, info, _recipeMetadataRegistry);
		var recipeLeft = ExecutionTimeEstimator.TimeLeftInRecipe(snapshot, info);

		TimeLeftInStepText = FormatTimeSpan(stepLeft);
		TimeLeftInRecipeText = FormatTimeSpan(recipeLeft);
	}
}
