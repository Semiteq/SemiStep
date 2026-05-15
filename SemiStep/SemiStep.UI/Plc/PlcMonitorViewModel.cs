using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.Plc;

public sealed class PlcMonitorViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	private bool _isRecipeActive;
	private int _actualLine;
	private string _stepElapsedTime = "0.0 s";
	private int _forLoopCount1;
	private int _forLoopCount2;
	private int _forLoopCount3;

	public PlcMonitorViewModel(RecipeCoordinator coordinator)
	{
		coordinator.ExecutionState
			.Subscribe(OnExecutionStateChanged)
			.DisposeWith(_disposables);
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

	public string StepElapsedTime
	{
		get => _stepElapsedTime;
		private set => this.RaiseAndSetIfChanged(ref _stepElapsedTime, value);
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

	private void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		IsRecipeActive = info.RecipeActive;
		ActualLine = info.ActualLine;
		StepElapsedTime = $"{info.StepCurrentTime:0.0} s";
		ForLoopCount1 = info.ForLoopCount1;
		ForLoopCount2 = info.ForLoopCount2;
		ForLoopCount3 = info.ForLoopCount3;
	}
}
