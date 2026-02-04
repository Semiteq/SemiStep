using Core.Analysis;
using Core.Entities;

using Domain.Facade;

namespace Tests.Core.Helpers;

/// <summary>
/// Fluent test driver for building and manipulating recipes in tests.
/// Wraps CoreService to provide convenient methods for common test scenarios.
/// </summary>
public sealed class RecipeTestDriver(DomainFacade domainFacade)
{

	/// <summary>
	/// Gets the current recipe snapshot.
	/// </summary>
	public RecipeSnapshot Snapshot => domainFacade.Snapshot;

	/// <summary>
	/// Gets the current recipe.
	/// </summary>
	public Recipe Recipe => domainFacade.CurrentRecipe;

	/// <summary>
	/// Gets whether the current recipe is valid.
	/// </summary>
	public bool IsValid => domainFacade.IsValid;

	/// <summary>
	/// Gets the step count of the current recipe.
	/// </summary>
	public int StepCount => Recipe.StepCount;

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

	/// <summary>
	/// Appends a Wait step with the specified duration.
	/// </summary>
	public RecipeTestDriver AddWait(float durationSeconds = 10f)
	{
		domainFacade.AppendStep(WaitActionId);
		var lastIndex = Recipe.StepCount - 1;
		domainFacade.UpdateStepProperty(lastIndex, StepDurationColumn, durationSeconds);
		return this;
	}

	/// <summary>
	/// Appends a ForLoop step with the specified iteration count.
	/// </summary>
	public RecipeTestDriver AddFor(int iterations)
	{
		domainFacade.AppendStep(ForLoopActionId);
		var lastIndex = Recipe.StepCount - 1;
		domainFacade.UpdateStepProperty(lastIndex, TaskColumn, (float)iterations);
		return this;
	}

	/// <summary>
	/// Appends an EndForLoop step.
	/// </summary>
	public RecipeTestDriver AddEndFor()
	{
		domainFacade.AppendStep(EndForLoopActionId);
		return this;
	}

	/// <summary>
	/// Appends a Pause step.
	/// </summary>
	public RecipeTestDriver AddPause()
	{
		domainFacade.AppendStep(PauseActionId);
		return this;
	}

	/// <summary>
	/// Appends a step with the specified action ID.
	/// </summary>
	public RecipeTestDriver AddStep(int actionId)
	{
		domainFacade.AppendStep(actionId);
		return this;
	}

	#endregion

	#region Insert Steps

	/// <summary>
	/// Inserts a Wait step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertWait(int index, float durationSeconds = 10f)
	{
		domainFacade.InsertStep(index, WaitActionId);
		domainFacade.UpdateStepProperty(index, StepDurationColumn, durationSeconds);
		return this;
	}

	/// <summary>
	/// Inserts a ForLoop step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertFor(int index, int iterations)
	{
		domainFacade.InsertStep(index, ForLoopActionId);
		domainFacade.UpdateStepProperty(index, TaskColumn, (float)iterations);
		return this;
	}

	/// <summary>
	/// Inserts an EndForLoop step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertEndFor(int index)
	{
		domainFacade.InsertStep(index, EndForLoopActionId);
		return this;
	}

	#endregion

	#region Modify Steps

	/// <summary>
	/// Sets the duration of a step.
	/// </summary>
	public RecipeTestDriver SetDuration(int index, float seconds)
	{
		domainFacade.UpdateStepProperty(index, StepDurationColumn, seconds);
		return this;
	}

	/// <summary>
	/// Sets the task (iterations) value of a step.
	/// </summary>
	public RecipeTestDriver SetTask(int index, float value)
	{
		domainFacade.UpdateStepProperty(index, TaskColumn, value);
		return this;
	}

	/// <summary>
	/// Changes the action of a step.
	/// </summary>
	public RecipeTestDriver ReplaceAction(int index, int actionId)
	{
		domainFacade.ChangeStepAction(index, actionId);
		return this;
	}

	/// <summary>
	/// Removes a step at the specified index.
	/// </summary>
	public RecipeTestDriver RemoveStep(int index)
	{
		domainFacade.RemoveStep(index);
		return this;
	}

	#endregion

	#region Recipe Management

	/// <summary>
	/// Creates a new empty recipe.
	/// </summary>
	public RecipeTestDriver NewRecipe()
	{
		domainFacade.NewRecipe();
		return this;
	}

	#endregion
}
