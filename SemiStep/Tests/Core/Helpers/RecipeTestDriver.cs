using Core.Analysis;
using Core.Entities;

using Domain.Services;

namespace Tests.Core.Helpers;

/// <summary>
/// Fluent test driver for building and manipulating recipes in tests.
/// Wraps CoreService to provide convenient methods for common test scenarios.
/// </summary>
public sealed class RecipeTestDriver
{
	private readonly CoreService _core;

	public RecipeTestDriver(CoreService core)
	{
		_core = core;
	}

	/// <summary>
	/// Gets the current recipe snapshot.
	/// </summary>
	public RecipeSnapshot Snapshot => _core.LastSnapshot
		?? throw new InvalidOperationException("No snapshot available. Call a mutation method first.");

	/// <summary>
	/// Gets the current recipe.
	/// </summary>
	public Recipe Recipe => _core.CurrentRecipe;

	/// <summary>
	/// Gets whether the current recipe is valid.
	/// </summary>
	public bool IsValid => _core.IsValid;

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
		_core.AppendStep(WaitActionId);
		var lastIndex = Recipe.StepCount - 1;
		_core.UpdateProperty(lastIndex, StepDurationColumn, durationSeconds);
		return this;
	}

	/// <summary>
	/// Appends a ForLoop step with the specified iteration count.
	/// </summary>
	public RecipeTestDriver AddFor(int iterations)
	{
		_core.AppendStep(ForLoopActionId);
		var lastIndex = Recipe.StepCount - 1;
		_core.UpdateProperty(lastIndex, TaskColumn, (float)iterations);
		return this;
	}

	/// <summary>
	/// Appends an EndForLoop step.
	/// </summary>
	public RecipeTestDriver AddEndFor()
	{
		_core.AppendStep(EndForLoopActionId);
		return this;
	}

	/// <summary>
	/// Appends a Pause step.
	/// </summary>
	public RecipeTestDriver AddPause()
	{
		_core.AppendStep(PauseActionId);
		return this;
	}

	/// <summary>
	/// Appends a step with the specified action ID.
	/// </summary>
	public RecipeTestDriver AddStep(int actionId)
	{
		_core.AppendStep(actionId);
		return this;
	}

	#endregion

	#region Insert Steps

	/// <summary>
	/// Inserts a Wait step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertWait(int index, float durationSeconds = 10f)
	{
		_core.InsertStep(index, WaitActionId);
		_core.UpdateProperty(index, StepDurationColumn, durationSeconds);
		return this;
	}

	/// <summary>
	/// Inserts a ForLoop step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertFor(int index, int iterations)
	{
		_core.InsertStep(index, ForLoopActionId);
		_core.UpdateProperty(index, TaskColumn, (float)iterations);
		return this;
	}

	/// <summary>
	/// Inserts an EndForLoop step at the specified index.
	/// </summary>
	public RecipeTestDriver InsertEndFor(int index)
	{
		_core.InsertStep(index, EndForLoopActionId);
		return this;
	}

	#endregion

	#region Modify Steps

	/// <summary>
	/// Sets the duration of a step.
	/// </summary>
	public RecipeTestDriver SetDuration(int index, float seconds)
	{
		_core.UpdateProperty(index, StepDurationColumn, seconds);
		return this;
	}

	/// <summary>
	/// Sets the task (iterations) value of a step.
	/// </summary>
	public RecipeTestDriver SetTask(int index, float value)
	{
		_core.UpdateProperty(index, TaskColumn, value);
		return this;
	}

	/// <summary>
	/// Changes the action of a step.
	/// </summary>
	public RecipeTestDriver ReplaceAction(int index, int actionId)
	{
		_core.ChangeStepAction(index, actionId);
		return this;
	}

	/// <summary>
	/// Removes a step at the specified index.
	/// </summary>
	public RecipeTestDriver RemoveStep(int index)
	{
		_core.RemoveStep(index);
		return this;
	}

	#endregion

	#region Recipe Management

	/// <summary>
	/// Creates a new empty recipe.
	/// </summary>
	public RecipeTestDriver NewRecipe()
	{
		_core.NewRecipe();
		return this;
	}

	#endregion
}
