using System.Collections.ObjectModel;

using SemiStep.Core.Plc.State;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ExecutionHighlightTracker
{
	private readonly ObservableCollection<RecipeRowViewModel> _rows;

	private bool _lastRecipeActive;
	private int _lastActualLine = -1;

	public ExecutionHighlightTracker(ObservableCollection<RecipeRowViewModel> rows)
	{
		_rows = rows;
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

		var previousLine = _lastActualLine;
		_lastRecipeActive = true;
		_lastActualLine = info.ActualLine;

		if (previousLine >= 0 && previousLine < _rows.Count && previousLine != info.ActualLine)
		{
			_rows[previousLine].IsCurrentStep = false;
			_rows[previousLine].IsPastStep = previousLine < info.ActualLine;
		}

		if (info.ActualLine >= 0 && info.ActualLine < _rows.Count)
		{
			_rows[info.ActualLine].IsCurrentStep = true;
			_rows[info.ActualLine].IsPastStep = false;
		}
	}

	public void Reset()
	{
		_lastRecipeActive = false;
		_lastActualLine = -1;
	}

	private void ClearAllStepHighlights()
	{
		foreach (var row in _rows)
		{
			row.IsCurrentStep = false;
			row.IsPastStep = false;
		}
	}
}
