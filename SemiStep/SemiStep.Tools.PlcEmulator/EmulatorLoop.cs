using SemiStep.Core.Plc.S7.Serialization;

namespace SemiStep.Tools.PlcEmulator;

internal sealed class EmulatorLoop(EmulatedPlc plc, RunEngine engine)
{
	private readonly ManagingAreaCodec _managingCodec = new(plc.Layout.ManagingDb);
	private readonly int[] _recipeDataDbNumbers =
		[plc.Layout.IntDb.DbNumber, plc.Layout.FloatDb.DbNumber, plc.Layout.StringDb.DbNumber];
	private bool _wasCommitted;
	private bool _recipeDataWritten;

	/// <summary>Raised on the tick thread.</summary>
	public event Action<EmulatorEvent>? EventRaised;

	public RunState State => engine.State;

	public async Task RunAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(RunEngine.TickInterval, timeProvider);
		try
		{
			while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
			{
				Tick();
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			EventRaised?.Invoke(new TickFailed(ex.Message));
		}
	}

	public void Tick()
	{
		foreach (var serverEvent in plc.PickEvents())
		{
			if (serverEvent is DataWritten written)
			{
				_recipeDataWritten |= _recipeDataDbNumbers.Contains(written.DbNumber);
			}
			else
			{
				EventRaised?.Invoke(serverEvent);
			}
		}

		ReportCommit();
		engine.Tick();
	}

	// SemiStep clears committed, writes the arrays and sets committed again within one tick, so a recommit
	// shows as array writes while committed reads true; the events are drained before DB995 is read.
	private void ReportCommit()
	{
		var managing = plc.Layout.ManagingDb;
		var state = _managingCodec.Decode(plc.Read(managing.DbNumber, 0, managing.TotalSize)).Value;
		if (state.Committed && (!_wasCommitted || _recipeDataWritten))
		{
			EventRaised?.Invoke(new RecipeCommitted(state.RecipeLines));
			_recipeDataWritten = false;
		}

		_wasCommitted = state.Committed;
	}
}
