using System.Globalization;

using Shared.Core;

namespace UI.ViewModels;

public partial class RecipeGridViewModel
{
	public void RebuildAllRows(Recipe recipe)
	{
		foreach (var row in RecipeRows)
		{
			row.Dispose();
		}

		RecipeRows.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var step = recipe.Steps[i];
			var action = ActionRegistry.GetAction(step.ActionKey);
			RecipeRows.Add(CreateRowViewModel(step, action, i + 1));
		}
	}

	public void InsertRow(int index, Step step)
	{
		var action = ActionRegistry.GetAction(step.ActionKey);
		RecipeRows.Insert(index, CreateRowViewModel(step, action, index + 1));
		RenumberRowsFrom(index + 1);
	}

	public void RemoveRow(int index)
	{
		RecipeRows[index].Dispose();
		RecipeRows.RemoveAt(index);
		RenumberRowsFrom(index);
	}

	public void RenumberRowsFrom(int startIndex)
	{
		for (var i = startIndex; i < RecipeRows.Count; i++)
		{
			RecipeRows[i].UpdateStepNumber(i + 1);
		}
	}

	public void RefreshStepStartTimes()
	{
		var stepStartTimes = _domainFacade.Snapshot.StepStartTimes;
		for (var i = 0; i < RecipeRows.Count; i++)
		{
			var rawSeconds = stepStartTimes.TryGetValue(i, out var time)
				? time.TotalSeconds.ToString(CultureInfo.InvariantCulture)
				: string.Empty;
			RecipeRows[i].UpdateStepStartTime(rawSeconds);
		}
	}

	public List<Step> CollectSelectedSteps()
	{
		var recipe = _domainFacade.CurrentRecipe;

		return _selectedRowIndices
			.OrderBy(i => i)
			.Select(i => recipe.Steps[i])
			.ToList();
	}

	public void InsertRowsForSteps(int startIndex, int count)
	{
		for (var i = 0; i < count; i++)
		{
			var step = _domainFacade.CurrentRecipe.Steps[startIndex + i];
			var action = ActionRegistry.GetAction(step.ActionKey);
			var rowVm = CreateRowViewModel(step, action, startIndex + i + 1);
			RecipeRows.Insert(startIndex + i, rowVm);
		}

		RenumberRowsFrom(startIndex + count);
	}

	private RecipeRowViewModel CreateRowViewModel(
		Step step,
		ActionDefinition action,
		int stepNumber)
	{
		return new RecipeRowViewModel(
			stepNumber,
			step,
			action,
			GroupRegistry,
			ColumnRegistry,
			PropertyRegistry,
			OnCellValueChanged,
			OnActionChanged);
	}

	private void AppendRow(Step step)
	{
		var action = ActionRegistry.GetAction(step.ActionKey);
		RecipeRows.Add(CreateRowViewModel(step, action, RecipeRows.Count + 1));
	}
}
