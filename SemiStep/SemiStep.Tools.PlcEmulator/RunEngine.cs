using FluentResults;

using SemiStep.Core.Plc.State;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>Runs the committed recipe per FB Recipe of the TIA_Plazma export, which may be stale.</summary>
internal sealed class RunEngine(EmulatedPlc plc, CommittedRecipeLoader loader)
{
	public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

	private static readonly TimeSpan _finishHold = TimeSpan.FromSeconds(1);
	private static readonly int _finishHoldTicks = (int)(_finishHold / TickInterval);
	private static readonly float _secondsPerTick = (float)TickInterval.TotalSeconds;

	private readonly Lock _gate = new();
	private readonly ExecutionAreaEncoder _encoder = new(plc.Layout.ExecutionDb);

	private Phase _phase = Phase.Idle;
	private RunPlan? _plan;
	private IEnumerator<ExecutionFrame>? _frames;
	private ExecutionFrame? _frame;
	private int _frameTicks;
	private int _finishTicks;
	private bool _continueRequested;
	private PlcExecutionInfo _state = PlcExecutionInfo.Empty;

	private enum Phase
	{
		Idle,
		Starting,
		Running,
		Finishing
	}

	public Result Run()
	{
		lock (_gate)
		{
			if (_phase != Phase.Idle)
			{
				return Result.Fail(new RunRefusal(RunRefusalReason.AlreadyRunning, "already running"));
			}

			var loaded = loader.Load();
			if (loaded.IsFailed)
			{
				return loaded.ToResult();
			}

			_plan = loaded.Value;
			_frames = _plan.Frames().GetEnumerator();
			_frame = null;
			_continueRequested = false;
			_phase = Phase.Starting;
			Publish(new PlcExecutionInfo(true, -1, 0f, 0, 0, 0));
			return Result.Ok();
		}
	}

	public Result Stop()
	{
		lock (_gate)
		{
			if (_phase is Phase.Idle or Phase.Finishing)
			{
				return Result.Fail(new RunRefusal(RunRefusalReason.NotRunning, "not running"));
			}

			BeginFinish(_state.ActualLine);
			return Result.Ok();
		}
	}

	public Result Continue()
	{
		lock (_gate)
		{
			if (_phase != Phase.Running || _frame is not { WaitsForOperator: true })
			{
				return Result.Fail(new RunRefusal(RunRefusalReason.NotPaused, "not waiting at a pause"));
			}

			_continueRequested = true;
			return Result.Ok();
		}
	}

	public RunState State
	{
		get
		{
			lock (_gate)
			{
				var lineCount = _plan?.LineCount ?? 0;
				var phase = _phase switch
				{
					Phase.Idle => RunPhase.Idle,
					Phase.Starting => RunPhase.Starting,
					Phase.Finishing => RunPhase.Finishing,
					_ when _frame is { WaitsForOperator: true } => RunPhase.Paused,
					_ => RunPhase.Running
				};
				return new RunState(phase, _state.ActualLine, lineCount);
			}
		}
	}

	public void Tick()
	{
		lock (_gate)
		{
			switch (_phase)
			{
				case Phase.Starting:
					AdvanceFrame();
					break;
				case Phase.Running:
					TickFrame();
					break;
				case Phase.Finishing:
					TickFinish();
					break;
			}
		}
	}

	private void TickFrame()
	{
		_frameTicks++;
		var frame = _frame!;
		var done = frame.WaitsForOperator ? _continueRequested : _frameTicks >= frame.Ticks;
		if (done)
		{
			AdvanceFrame();
			return;
		}

		Publish(_state with { StepCurrentTime = _frameTicks * _secondsPerTick });
	}

	private void AdvanceFrame()
	{
		if (!_frames!.MoveNext())
		{
			BeginFinish(_plan!.LineCount);
			return;
		}

		_frame = _frames.Current;
		_frameTicks = 0;
		_continueRequested = false;
		_phase = Phase.Running;
		Publish(new PlcExecutionInfo(
			true,
			_frame.Line,
			0f,
			_frame.ForLoopCount1,
			_frame.ForLoopCount2,
			_frame.ForLoopCount3));
	}

	private void BeginFinish(int actualLine)
	{
		_phase = Phase.Finishing;
		_finishTicks = 0;
		_frame = null;
		Publish(new PlcExecutionInfo(true, actualLine, 0f, 0, 0, 0));
	}

	private void TickFinish()
	{
		_finishTicks++;
		if (_finishTicks < _finishHoldTicks)
		{
			return;
		}

		_phase = Phase.Idle;
		_frames?.Dispose();
		_frames = null;
		Publish(_state with { RecipeActive = false });
	}

	private void Publish(PlcExecutionInfo state)
	{
		_state = state;
		plc.Write(plc.Layout.ExecutionDb.DbNumber, 0, _encoder.Encode(state));
	}
}
