using SemiStep.Core.Plc.State;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Drives the running, past and stop states of the rows. The run's tints outlive the run and clear on the
/// next edit or load made while no run is active. See docs/architecture/recipe-grid-surface.md.
/// </summary>
internal sealed class ExecutionHighlightTracker(Func<int> rowCount, Func<int, RecipeRowViewModel> rowAt)
{
	private const int NoLine = -1;

	private readonly Func<int> _rowCount = rowCount;
	private readonly Func<int, RecipeRowViewModel> _rowAt = rowAt;

	private bool _lastRecipeActive;
	private int _lastActiveLine = NoLine;
	private bool _hasRunTints;

	public void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		if (!info.RecipeActive)
		{
			if (_lastRecipeActive)
			{
				// The PLC reports no end reason: a line at or past the step count is a finished run, a line on a step
				// is a stop there. The inactive emission's own line counts only past the last step, because Empty
				// (link loss, sync off) carries line 0.
				var runFinished = info.ActualLine >= _rowCount();
				ShowRunEnd(runFinished ? info.ActualLine : _lastActiveLine);
			}

			_lastRecipeActive = false;
			return;
		}

		if (!_lastRecipeActive)
		{
			// A run start, or the first emission after a rebuild: earlier tints may sit on any row.
			ClearAllChangedHighlights();
			ShowRunning(info.ActualLine, 0, _rowCount() - 1);
		}
		else if (info.ActualLine == _lastActiveLine)
		{
			return;
		}
		else
		{
			// Rows above both lines stay past and rows below both stay idle, so only the span between them changes.
			ShowRunning(
				info.ActualLine,
				Math.Min(info.ActualLine, _lastActiveLine),
				Math.Max(info.ActualLine, _lastActiveLine));
		}

		_lastRecipeActive = true;
		_lastActiveLine = info.ActualLine;
	}

	public void ClearRunTintsWhenIdle()
	{
		if (_lastRecipeActive || !_hasRunTints)
		{
			return;
		}

		var rowCount = _rowCount();
		for (var i = 0; i < rowCount; i++)
		{
			SetState(_rowAt(i), isCurrent: false, isPast: false, isStop: false);
		}

		_hasRunTints = false;
	}

	public void Reset()
	{
		_lastRecipeActive = false;
		_lastActiveLine = NoLine;
		_hasRunTints = false;
	}

	private void ShowRunning(int line, int firstRow, int lastRow)
	{
		var end = Math.Min(lastRow, _rowCount() - 1);
		for (var i = Math.Max(firstRow, 0); i <= end; i++)
		{
			SetState(_rowAt(i), isCurrent: i == line, isPast: i < line, isStop: false);
		}

		_hasRunTints = true;
	}

	private void ShowRunEnd(int lastLine)
	{
		var rowCount = _rowCount();
		var stoppedOnStep = lastLine >= 0 && lastLine < rowCount;
		for (var i = 0; i < rowCount; i++)
		{
			SetState(_rowAt(i), isCurrent: false, isPast: i <= lastLine, isStop: stoppedOnStep && i == lastLine);
		}

		_hasRunTints = lastLine >= 0;
	}

	private static void SetState(RecipeRowViewModel row, bool isCurrent, bool isPast, bool isStop)
	{
		row.IsCurrentStep = isCurrent;
		row.IsPastStep = isPast;
		row.IsStopStep = isStop;
	}

	private void ClearAllChangedHighlights()
	{
		var rowCount = _rowCount();
		for (var i = 0; i < rowCount; i++)
		{
			_rowAt(i).ClearAllChanged();
		}
	}
}
