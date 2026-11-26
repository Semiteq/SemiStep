using System.Globalization;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes.Analysis;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Writes the row columns derived from the recipe snapshot: start time, loop depth and loop pass text.</summary>
internal sealed class RowAnnotationRefresher(
	RecipeCoordinator coordinator,
	Func<int> rowCount,
	Func<int, RecipeRowViewModel> rowAt)
{
	private PlcExecutionInfo _loopExecutionInfo = PlcExecutionInfo.Empty;

	/// <summary>Start times refresh from <paramref name="startTimesFromIndex"/>; depths and passes always in full.</summary>
	public void Refresh(int startTimesFromIndex)
	{
		RefreshStepStartTimes(startTimesFromIndex);
		RefreshLoopDepths();
		RefreshLoopPasses();
	}

	public void OnLoopExecutionChanged(PlcExecutionInfo info)
	{
		_loopExecutionInfo = info;
		RefreshLoopPasses();
	}

	private void RefreshStepStartTimes(int fromIndex)
	{
		var stepStartTimes = coordinator.Snapshot.StepStartTimes;
		for (var i = fromIndex; i < rowCount(); i++)
		{
			string formattedTime;
			if (i < stepStartTimes.Count)
			{
				var rawSeconds = stepStartTimes[i].TotalSeconds.ToString(CultureInfo.InvariantCulture);
				formattedTime = TimeFormatHelper.FormatValue(
					rawSeconds,
					TimeFormatHelper.TimeHmsFormat,
					units: null);
			}
			else
			{
				formattedTime = string.Empty;
			}

			rowAt(i).UpdateStepStartTime(formattedTime);
		}
	}

	private void RefreshLoopDepths()
	{
		var rowLoopDepths = coordinator.Snapshot.RowLoopDepths;
		for (var i = 0; i < rowCount(); i++)
		{
			rowAt(i).ForDepth = Math.Min(rowLoopDepths[i], 3);
		}
	}

	private void RefreshLoopPasses()
	{
		var passTextByStartRow = new Dictionary<int, string>();
		foreach (var active in ActiveLoops.Resolve(coordinator.Snapshot, _loopExecutionInfo))
		{
			passTextByStartRow[active.Loop.StartIndex] = string.Create(
				CultureInfo.InvariantCulture,
				$"({active.Pass}/{active.Loop.Iterations})");
		}

		for (var i = 0; i < rowCount(); i++)
		{
			rowAt(i).LoopPassText = passTextByStartRow.GetValueOrDefault(i);
		}
	}
}
