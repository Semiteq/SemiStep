using System.Collections.ObjectModel;

using SemiStep.Core.Plc.State;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedExecutionHighlightTracker
{
	private readonly ObservableCollection<StepColumnViewModel> _stepColumns;

	private bool _lastRecipeActive;
	private int _lastActualLine = -1;

	public TransposedExecutionHighlightTracker(ObservableCollection<StepColumnViewModel> stepColumns)
	{
		_stepColumns = stepColumns;
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

		for (var i = 0; i < _stepColumns.Count; i++)
		{
			_stepColumns[i].Row.IsCurrentStep = i == info.ActualLine;
			_stepColumns[i].Row.IsPastStep = i < info.ActualLine;
		}
	}

	public void Reset()
	{
		_lastRecipeActive = false;
		_lastActualLine = -1;
	}

	private void ClearAllStepHighlights()
	{
		foreach (var stepColumn in _stepColumns)
		{
			stepColumn.Row.IsCurrentStep = false;
			stepColumn.Row.IsPastStep = false;
		}
	}

	private void ClearAllChangedHighlights()
	{
		foreach (var stepColumn in _stepColumns)
		{
			stepColumn.Row.ClearAllChanged();
		}
	}
}
