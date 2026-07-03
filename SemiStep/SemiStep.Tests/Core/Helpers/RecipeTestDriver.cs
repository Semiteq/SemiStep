using System.Globalization;

using FluentResults;

using SemiStep.Core.Recipes;
using SemiStep.Core.Shared;

namespace SemiStep.Tests.Core.Helpers;

public sealed class RecipeTestDriver(RecipeSession session)
{
	public RecipeSnapshot Snapshot => session.Snapshot.Value;

	public Recipe Recipe => session.Current;

	public bool IsValid => session.IsValid;

	public int StepCount => Recipe.StepCount;

	public IReadOnlyList<IError> Errors => session.Snapshot
		.Reasons
		.OfType<IError>()
		.ToList();

	public IReadOnlyList<string> Warnings => session.Snapshot
		.Reasons
		.OfType<Warning>()
		.Select(w => w.Message)
		.ToList();

	#region Recipe Management

	public RecipeTestDriver NewRecipe()
	{
		session.Reset();

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
		session.AppendStep(WaitActionId);
		var lastIndex = Recipe.StepCount - 1;
		session.UpdateStepProperty(lastIndex, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddFor(int iterations)
	{
		session.AppendStep(ForLoopActionId);
		var lastIndex = Recipe.StepCount - 1;
		session.UpdateStepProperty(lastIndex, TaskColumn, ((float)iterations).ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddEndFor()
	{
		session.AppendStep(EndForLoopActionId);

		return this;
	}

	public RecipeTestDriver AddPause()
	{
		session.AppendStep(PauseActionId);

		return this;
	}

	#endregion

	#region Insert Steps

	public RecipeTestDriver InsertWait(int index, float durationSeconds = 10f)
	{
		session.InsertStep(index, WaitActionId);
		session.UpdateStepProperty(index, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	#endregion

	#region Modify Steps

	public RecipeTestDriver SetDuration(int index, float seconds)
	{
		session.UpdateStepProperty(index, StepDurationColumn, seconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver SetTask(int index, float value)
	{
		session.UpdateStepProperty(index, TaskColumn, value.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver ReplaceAction(int index, int actionId)
	{
		session.ChangeStepAction(index, actionId);

		return this;
	}

	public RecipeTestDriver RemoveStep(int index)
	{
		session.RemoveStep(index);

		return this;
	}

	public RecipeTestDriver InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		session.InsertSteps(startIndex, steps);

		return this;
	}

	public RecipeTestDriver RemoveSteps(IReadOnlyList<int> indices)
	{
		session.RemoveSteps(indices);

		return this;
	}

	#endregion
}
