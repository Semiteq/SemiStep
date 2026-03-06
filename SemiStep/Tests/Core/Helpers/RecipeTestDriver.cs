using System.Globalization;

using Core.Analysis;
using Core.Entities;

using Domain.Facade;

namespace Tests.Core.Helpers;

public sealed class RecipeTestDriver(DomainFacade domainFacade)
{
	public RecipeSnapshot Snapshot => domainFacade.Snapshot;

	public Recipe Recipe => domainFacade.CurrentRecipe;

	public bool IsValid => domainFacade.IsValid;

	public int StepCount => Recipe.StepCount;

	#region Recipe Management

	public RecipeTestDriver NewRecipe()
	{
		domainFacade.NewRecipe();

		return this;
	}

	#endregion

	#region Service Action IDs

	public const int WaitActionId = 10;
	public const int ForLoopActionId = 20;
	public const int EndForLoopActionId = 30;
	public const int PauseActionId = 40;

	#endregion

	#region Column Keys

	public const string StepDurationColumn = "step_duration";
	public const string TaskColumn = "task";
	public const string CommentColumn = "comment";

	#endregion

	#region Add Steps

	public RecipeTestDriver AddWait(float durationSeconds = 10f)
	{
		domainFacade.AppendStep(WaitActionId);
		var lastIndex = Recipe.StepCount - 1;
		domainFacade.UpdateStepProperty(lastIndex, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddFor(int iterations)
	{
		domainFacade.AppendStep(ForLoopActionId);
		var lastIndex = Recipe.StepCount - 1;
		domainFacade.UpdateStepProperty(lastIndex, TaskColumn, ((float)iterations).ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver AddEndFor()
	{
		domainFacade.AppendStep(EndForLoopActionId);

		return this;
	}

	public RecipeTestDriver AddPause()
	{
		domainFacade.AppendStep(PauseActionId);

		return this;
	}

	public RecipeTestDriver AddStep(int actionId)
	{
		domainFacade.AppendStep(actionId);

		return this;
	}

	#endregion

	#region Insert Steps

	public RecipeTestDriver InsertWait(int index, float durationSeconds = 10f)
	{
		domainFacade.InsertStep(index, WaitActionId);
		domainFacade.UpdateStepProperty(index, StepDurationColumn, durationSeconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver InsertFor(int index, int iterations)
	{
		domainFacade.InsertStep(index, ForLoopActionId);
		domainFacade.UpdateStepProperty(index, TaskColumn, ((float)iterations).ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver InsertEndFor(int index)
	{
		domainFacade.InsertStep(index, EndForLoopActionId);

		return this;
	}

	#endregion

	#region Modify Steps

	public RecipeTestDriver SetDuration(int index, float seconds)
	{
		domainFacade.UpdateStepProperty(index, StepDurationColumn, seconds.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver SetTask(int index, float value)
	{
		domainFacade.UpdateStepProperty(index, TaskColumn, value.ToString(CultureInfo.InvariantCulture));

		return this;
	}

	public RecipeTestDriver ReplaceAction(int index, int actionId)
	{
		domainFacade.ChangeStepAction(index, actionId);

		return this;
	}

	public RecipeTestDriver RemoveStep(int index)
	{
		domainFacade.RemoveStep(index);

		return this;
	}

	#endregion
}
