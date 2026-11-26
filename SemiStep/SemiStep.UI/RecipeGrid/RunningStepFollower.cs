using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using SemiStep.Core.Plc.State;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Asks the view to keep the running step in view, except for 10 s after each operator scroll input; when that
/// pause ends during a run, it asks for the running step at once. It never touches selection or focus.
/// </summary>
internal sealed class RunningStepFollower(IScheduler scheduler) : IDisposable
{
	public static readonly TimeSpan PauseAfterOperatorScroll = TimeSpan.FromSeconds(10);

	private readonly IScheduler _scheduler = scheduler;
	private readonly Subject<int> _scrollRequests = new();
	private readonly SerialDisposable _pauseEnd = new();
	private int? _runningStep;
	private bool _isPaused;

	/// <summary>
	/// A view that subscribes mid-run, such as after an orientation flip, first gets the running step, unless a pause
	/// is on; then every later request.
	/// </summary>
	public IObservable<int> ScrollRequests => Observable.Defer(() =>
		_runningStep is { } step && !_isPaused
			? _scrollRequests.StartWith(step)
			: _scrollRequests.AsObservable());

	public void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		int? runningStep = info.RecipeActive && info.ActualLine >= 0 ? info.ActualLine : null;
		if (runningStep == _runningStep)
		{
			return;
		}

		_runningStep = runningStep;
		if (runningStep is not { } step)
		{
			EndPause();
			return;
		}

		if (!_isPaused)
		{
			_scrollRequests.OnNext(step);
		}
	}

	public void OnOperatorScroll()
	{
		if (_runningStep is null)
		{
			return;
		}

		_isPaused = true;
		_pauseEnd.Disposable = _scheduler.Schedule(PauseAfterOperatorScroll, ResumeFollowing);
	}

	public void Dispose()
	{
		_pauseEnd.Dispose();
		_scrollRequests.Dispose();
	}

	private void ResumeFollowing()
	{
		_isPaused = false;
		if (_runningStep is { } step)
		{
			_scrollRequests.OnNext(step);
		}
	}

	private void EndPause()
	{
		_isPaused = false;
		_pauseEnd.Disposable = null;
	}
}
