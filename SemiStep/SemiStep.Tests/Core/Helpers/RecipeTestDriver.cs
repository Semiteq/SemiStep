using System.Globalization;

using FluentResults;

using SemiStep.Core.Recipes;
using SemiStep.Core.Shared;

namespace SemiStep.Tests.Core.Helpers;

public sealed class RecipeTestDriver(RecipeWorkspace workspace, RecipeEditor editor)
{
	public RecipeSnapshot Snapshot => workspace.Snapshot.Value;

	public Recipe Recipe => workspace.CurrentRecipe;

	public bool IsValid => workspace.IsValid;

	public int StepCount => Recipe.StepCount;

	public IReadOnlyList<IError> Errors => workspace.Snapshot
		.Reasons
		.OfType<IError>()
		.ToList();

	public IReadOnlyList<string> Warnings => workspace.Snapshot
		.Reasons
		.OfType<Warning>()
		.Select(w => w.Message)
		.ToList();

	#region Recipe Management

	public RecipeTestDriver NewRecipe()
	{
		workspace.Reset();

		return this;
	}

	#endregion

	#region Service Action IDs

	public const int WaitActionId = 10;
	public const int ForLoopActionId = 20;
	public const int EndForLoopActionId = 30;
	public const int PauseActionId = 40;
	public const int WithGroupActionId = 50;

	#endregion

	#region Column Keys

	public const string StepDurationColumn = "step_duration";
	public const string TaskColumn = "task";
	public const string CommentColumn = "comment";
	public const string TargetColumn = "target";

	#endregion

	#region Add Steps

	public RecipeTestDriver AddWait(float durationSeconds = 10f)
	{
		editor.AppendStep(WaitActionId);
		var lastIndex = Recipe.StepCount - 1;
		editor.UpdateStepProperty(lastIndex, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddFor(int iterations)
	{
		editor.AppendStep(ForLoopActionId);
		var lastIndex = Recipe.StepCount - 1;
		editor.UpdateStepProperty(lastIndex, TaskColumn, ((float)iterations).ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddEndFor()
	{
		editor.AppendStep(EndForLoopActionId);

		return this;
	}

	public RecipeTestDriver AddPause()
	{
		editor.AppendStep(PauseActionId);

		return this;
	}

	public RecipeTestDriver AddStep(int actionId)
	{
		editor.AppendStep(actionId);

		return this;
	}

	#endregion

	#region Insert Steps

	public RecipeTestDriver InsertWait(int index, float durationSeconds = 10f)
	{
		editor.InsertStep(index, WaitActionId);
		editor.UpdateStepProperty(index, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver InsertFor(int index, int iterations)
	{
		editor.InsertStep(index, ForLoopActionId);
		editor.UpdateStepProperty(index, TaskColumn, ((float)iterations).ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver InsertEndFor(int index)
	{
		editor.InsertStep(index, EndForLoopActionId);

		return this;
	}

	#endregion

	#region Modify Steps

	public RecipeTestDriver SetDuration(int index, float seconds)
	{
		editor.UpdateStepProperty(index, StepDurationColumn, seconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver SetTask(int index, float value)
	{
		editor.UpdateStepProperty(index, TaskColumn, value.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver ReplaceAction(int index, int actionId)
	{
		editor.ChangeStepAction(index, actionId);

		return this;
	}

	public RecipeTestDriver RemoveStep(int index)
	{
		editor.RemoveStep(index);

		return this;
	}

	public RecipeTestDriver InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		editor.InsertSteps(startIndex, steps);

		return this;
	}

	public RecipeTestDriver RemoveSteps(IReadOnlyList<int> indices)
	{
		editor.RemoveSteps(indices);

		return this;
	}

	#endregion
}
