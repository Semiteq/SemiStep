using SemiStep.Core.Plc.State;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ExecutionHighlightTracker
{
	private readonly Func<int> _rowCount;
	private readonly Func<int, RecipeRowViewModel> _rowAt;

	private bool _lastRecipeActive;
	private int _lastActualLine = -1;

	public ExecutionHighlightTracker(Func<int> rowCount, Func<int, RecipeRowViewModel> rowAt)
	{
		_rowCount = rowCount;
		_rowAt = rowAt;
	}

	public void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		var activeChanged = info.RecipeActive != _lastRecipeActive;
		var lineChanged = info.ActualLine != _lastActualLine;

		if (!activeChanged && !lineChanged)
		{
			return;
		}

		if (!info.RecipeActive)
		{
			if (_lastRecipeActive)
			{
				ClearAllStepHighlights();
			}

			_lastRecipeActive = false;
			_lastActualLine = -1;

			return;
		}

		if (!_lastRecipeActive)
		{
			ClearAllChangedHighlights();
		}

		_lastRecipeActive = true;
		_lastActualLine = info.ActualLine;

		var rowCount = _rowCount();
		for (var i = 0; i < rowCount; i++)
		{
			var row = _rowAt(i);
			row.IsCurrentStep = i == info.ActualLine;
			row.IsPastStep = i < info.ActualLine;
		}
	}

	public void Reset()
	{
		_lastRecipeActive = false;
		_lastActualLine = -1;
	}

	private void ClearAllStepHighlights()
	{
		var rowCount = _rowCount();
		for (var i = 0; i < rowCount; i++)
		{
			var row = _rowAt(i);
			row.IsCurrentStep = false;
			row.IsPastStep = false;
		}
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
