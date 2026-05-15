using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.Core.Recipes;

/// <summary>
/// Owns the live recipe along with its undo/redo history, dirty-flag state, and
/// the mutation methods that act upon it. Consolidates the responsibilities of
/// <c>RecipeWorkspace</c>, <c>RecipeStateManager</c>, <c>RecipeHistoryManager</c>,
/// and <c>RecipeEditor</c> into a single class so that recipe mutations and the
/// state transitions they imply are expressed in one place.
/// </summary>
public sealed class RecipeSession
{
	private const int MaxHistoryDepth = 100;

	private readonly RecipeAnalyzer _analyzer;
	private readonly ILogger<RecipeSession> _logger;
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry;
	private readonly List<Recipe> _redoStack = new(MaxHistoryDepth);
	private readonly IPlcSyncService _syncService;
	private readonly List<Recipe> _undoStack = new(MaxHistoryDepth);

	private bool _isDirty;
	private Recipe _lastValidRecipe = Recipe.Empty;
	private Result<RecipeSnapshot> _latestSnapshot = Result.Ok(RecipeSnapshot.Empty);

	public RecipeSession(
		RecipeAnalyzer analyzer,
		RecipeMetadataRegistry recipeMetadataRegistry,
		IPlcSyncService syncService,
		ILogger<RecipeSession> logger)
	{
		_analyzer = analyzer;
		_recipeMetadataRegistry = recipeMetadataRegistry;
		_syncService = syncService;
		_logger = logger;
	}

	public Recipe Current => _latestSnapshot.IsSuccess
		? _latestSnapshot.Value.Recipe
		: Recipe.Empty;

	public Recipe LastValidRecipe => _lastValidRecipe;

	public bool IsDirty => _isDirty;

	public bool IsValid => _latestSnapshot.IsSuccess;

	public Result<RecipeSnapshot> Snapshot => _latestSnapshot;

	public bool CanUndo => _undoStack.Count > 0;

	public bool CanRedo => _redoStack.Count > 0;

	public Result Apply(Recipe newRecipe)
	{
		var snapshot = _analyzer.Analyze(newRecipe);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		PushHistory(Current);
		UpdateSnapshot(snapshot);
		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Undo()
	{
		if (_undoStack.Count == 0)
		{
			_logger.LogInformation("Undo requested but no state available");
			return Result.Fail("No state to undo to");
		}

		var previousIndex = _undoStack.Count - 1;
		var previous = _undoStack[previousIndex];

		var snapshot = _analyzer.Analyze(previous);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		_redoStack.Add(Current);
		_undoStack.RemoveAt(previousIndex);
		UpdateSnapshot(snapshot);

		NotifySyncIfEnabled();

		_logger.LogInformation("Undo applied: restored recipe with {StepCount} steps", Current.StepCount);

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Redo()
	{
		if (_redoStack.Count == 0)
		{
			_logger.LogInformation("Redo requested but no state available");
			return Result.Fail("No state to redo to");
		}

		var nextIndex = _redoStack.Count - 1;
		var next = _redoStack[nextIndex];

		var snapshot = _analyzer.Analyze(next);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		_undoStack.Add(Current);
		_redoStack.RemoveAt(nextIndex);
		UpdateSnapshot(snapshot);

		NotifySyncIfEnabled();

		_logger.LogInformation("Redo applied: restored recipe with {StepCount} steps", Current.StepCount);

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Reset()
	{
		ClearHistory();
		ResetSnapshotToEmpty();

		var snapshot = _analyzer.Analyze(Recipe.Empty);
		UpdateSnapshot(snapshot, markDirty: false);

		if (snapshot.IsFailed)
		{
			_logger.LogWarning("Empty recipe analysis unexpectedly failed: {Errors}",
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
		}

		NotifySyncIfEnabled();

		_logger.LogInformation("Recipe session reset to empty");

		return snapshot.ToResult();
	}

	public void MarkSaved()
	{
		_isDirty = false;
		_logger.LogInformation("Recipe marked as saved");
	}

	/// <summary>
	/// Replaces the current recipe with <paramref name="recipe"/> as a fresh editing session.
	/// Differs from <see cref="Apply"/>: clears undo history (does not push the previous state)
	/// because the new recipe represents a load (from CSV file or PLC), not an incremental edit.
	/// Differs from <see cref="Reset"/>: loads a specific recipe instead of <see cref="Recipe.Empty"/>.
	/// </summary>
	public Result LoadAsCurrent(Recipe recipe)
	{
		ClearHistory();
		var snapshot = _analyzer.Analyze(recipe);
		UpdateSnapshot(snapshot);
		NotifySyncIfEnabled();

		if (snapshot.IsFailed)
		{
			_logger.LogWarning(
				"Loaded recipe failed analysis: StepCount={StepCount}, Errors={Errors}",
				recipe.StepCount,
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
			return snapshot.ToResult();
		}

		_logger.LogInformation("Recipe loaded as current: {StepCount} steps", recipe.StepCount);

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	/// <summary>
	/// Validates an untrusted recipe via <paramref name="validator"/> and, on success, loads it
	/// as the current recipe. On validation failure, no state is mutated and the validation
	/// <see cref="Result"/> is returned. Used by all untrusted-recipe ingress points
	/// (CSV file load, PLC read, reconnect reconciliation) to avoid duplicating the
	/// validate-then-load pattern at each call site.
	/// </summary>
	public Result LoadAsCurrentValidated(Recipe recipe, ImportedRecipeValidator validator)
	{
		var validation = validator.Validate(recipe);
		if (validation.IsFailed)
		{
			return validation;
		}

		return LoadAsCurrent(recipe);
	}

	public Result<MutationOutcome> AppendStep(int actionId)
	{
		_logger.LogInformation(
			"Mutation entry: AppendStep ActionId={ActionId}, StepCount={StepCount}",
			actionId,
			Current.StepCount);

		var actionResult = _recipeMetadataRegistry.GetAction(actionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<MutationOutcome>();
		}

		var step = StepInitializer.Create(actionResult.Value, _recipeMetadataRegistry);
		var newRecipe = Current.AppendStep(step);

		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		var lastIndex = Current.StepCount - 1;
		return Result.Ok(new MutationOutcome(lastIndex))
			.WithReasons(applyResult.Reasons);
	}

	public Result<MutationOutcome> InsertStep(int index, int actionId)
	{
		_logger.LogInformation(
			"Mutation entry: InsertStep StepIndex={StepIndex}, ActionId={ActionId}, StepCount={StepCount}",
			index,
			actionId,
			Current.StepCount);

		var indexCheck = ValidateInsertIndex(Current, index);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		var actionResult = _recipeMetadataRegistry.GetAction(actionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<MutationOutcome>();
		}

		var step = StepInitializer.Create(actionResult.Value, _recipeMetadataRegistry);
		var newRecipe = Current.InsertStep(index, step);

		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		return Result.Ok(new MutationOutcome(index))
			.WithReasons(applyResult.Reasons);
	}

	public Result<MutationOutcome> RemoveStep(int index)
	{
		_logger.LogInformation(
			"Mutation entry: RemoveStep StepIndex={StepIndex}, StepCount={StepCount}",
			index,
			Current.StepCount);

		var indexCheck = ValidateStepIndex(Current, index);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		var newRecipe = Current.RemoveStep(index);
		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		var stepCount = Current.StepCount;
		int? suggested = stepCount > 0 ? Math.Min(index, stepCount - 1) : null;
		return Result.Ok(new MutationOutcome(suggested))
			.WithReasons(applyResult.Reasons);
	}

	public Result<MutationOutcome> RemoveSteps(IReadOnlyList<int> indices)
	{
		var distinctIndices = indices.Distinct().ToList();

		_logger.LogInformation(
			"Mutation entry: RemoveSteps Count={Count}, StepCount={StepCount}",
			distinctIndices.Count,
			Current.StepCount);

		var current = Current;
		foreach (var i in distinctIndices)
		{
			var indexCheck = ValidateStepIndex(current, i);
			if (indexCheck.IsFailed)
			{
				return indexCheck.ToResult<MutationOutcome>();
			}
		}

		var newRecipe = current.RemoveSteps(distinctIndices);
		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		var stepCount = Current.StepCount;
		int? suggested = stepCount > 0 ? Math.Min(distinctIndices.Min(), stepCount - 1) : null;
		return Result.Ok(new MutationOutcome(suggested))
			.WithReasons(applyResult.Reasons);
	}

	public Result<MutationOutcome> InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		_logger.LogInformation(
			"Mutation entry: InsertSteps StartIndex={StartIndex}, Count={Count}, StepCount={StepCount}",
			startIndex,
			steps.Count,
			Current.StepCount);

		var indexCheck = ValidateInsertIndex(Current, startIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		var newRecipe = Current.InsertSteps(startIndex, steps);
		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		return Result.Ok(new MutationOutcome(startIndex))
			.WithReasons(applyResult.Reasons);
	}

	public Result<MutationOutcome> ChangeStepAction(int stepIndex, int newActionId)
	{
		_logger.LogInformation(
			"Mutation entry: ChangeStepAction StepIndex={StepIndex}, ActionId={ActionId}, StepCount={StepCount}",
			stepIndex,
			newActionId,
			Current.StepCount);

		var indexCheck = ValidateStepIndex(Current, stepIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		var actionResult = _recipeMetadataRegistry.GetAction(newActionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<MutationOutcome>();
		}

		var step = StepInitializer.Create(actionResult.Value, _recipeMetadataRegistry);
		var newRecipe = Current.ReplaceStep(stepIndex, step);

		var applyResult = Apply(newRecipe);
		if (applyResult.IsFailed)
		{
			return applyResult.ToResult<MutationOutcome>();
		}

		return Result.Ok(new MutationOutcome(stepIndex))
			.WithReasons(applyResult.Reasons);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		_logger.LogInformation(
			"Mutation entry: UpdateStepProperty StepIndex={StepIndex}, ColumnKey={ColumnKey}, StepCount={StepCount}",
			stepIndex,
			columnKey,
			Current.StepCount);

		var indexCheck = ValidateStepIndex(Current, stepIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var current = Current;
		var step = current.Steps[stepIndex];

		var actionResult = _recipeMetadataRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var action = actionResult.Value;

		var actionPropertyResult = action.FindProperty(columnKey);
		if (actionPropertyResult.IsFailed)
		{
			return actionPropertyResult.ToResult();
		}

		var actionProperty = actionPropertyResult.Value;

		var propertyDefinitionResult = _recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		if (propertyDefinitionResult.IsFailed)
		{
			return propertyDefinitionResult.ToResult();
		}

		var parseResult = PropertyParser.Parse(value, propertyDefinitionResult.Value);
		if (parseResult.IsFailed)
		{
			return parseResult.ToResult();
		}

		var parsedValue = parseResult.Value;

		var typeCheck = PropertyValidator.Validate(propertyDefinitionResult.Value, parsedValue.Value);
		if (typeCheck.IsFailed)
		{
			return typeCheck;
		}

		var groupCheck = PropertyValidator.ValidateGroupValue(actionProperty, parsedValue, _recipeMetadataRegistry);
		if (groupCheck.IsFailed)
		{
			return groupCheck;
		}

		var updatedStep = step.WithProperty(columnKey, parsedValue);
		var newRecipe = current.ReplaceStep(stepIndex, updatedStep);

		return Apply(newRecipe);
	}

	private void UpdateSnapshot(Result<RecipeSnapshot> snapshot)
	{
		UpdateSnapshot(snapshot, markDirty: true);
	}

	private void UpdateSnapshot(Result<RecipeSnapshot> snapshot, bool markDirty)
	{
		_latestSnapshot = snapshot;

		if (markDirty)
		{
			_isDirty = true;
		}

		if (snapshot.IsSuccess)
		{
			_lastValidRecipe = snapshot.Value.Recipe;
		}
	}

	private void ResetSnapshotToEmpty()
	{
		_latestSnapshot = RecipeSnapshot.Empty;
		_lastValidRecipe = Recipe.Empty;
		_isDirty = false;
	}

	private void PushHistory(Recipe recipe)
	{
		_redoStack.Clear();

		if (_undoStack.Count >= MaxHistoryDepth)
		{
			_undoStack.RemoveAt(0);
		}

		_undoStack.Add(recipe);
	}

	private void ClearHistory()
	{
		_undoStack.Clear();
		_redoStack.Clear();
	}

	private void NotifySyncIfEnabled()
	{
		if (_syncService.IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(Current, IsValid);
		}
	}

	private static Result ValidateInsertIndex(Recipe recipe, int index)
	{
		if (index < 0 || index > recipe.Steps.Count)
		{
			return Result.Fail($"Insert index {index} is out of range for recipe with {recipe.Steps.Count} steps");
		}

		return Result.Ok();
	}

	private static Result ValidateStepIndex(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.Steps.Count)
		{
			return Result.Fail($"Step index {index} is out of range for recipe with {recipe.Steps.Count} steps");
		}

		return Result.Ok();
	}
}
