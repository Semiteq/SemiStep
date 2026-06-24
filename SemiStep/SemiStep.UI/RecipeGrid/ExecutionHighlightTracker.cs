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

		if (!_lastRecipeActive)
		{
			ClearAllChangedHighlights();
		}

		_lastRecipeActive = true;
		_lastActualLine = info.ActualLine;

		for (var i = 0; i < _rows.Count; i++)
		{
			_rows[i].IsCurrentStep = i == info.ActualLine;
			_rows[i].IsPastStep = i < info.ActualLine;
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

	private void ClearAllChangedHighlights()
	{
		foreach (var row in _rows)
		{
			row.ClearAllChanged();
		}
	}
}
