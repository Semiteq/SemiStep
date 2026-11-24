using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes;

/// <summary>
/// Owns the live recipe, its dirty flag and the mutations that produce new revisions; runs analyzer and
/// formula passes and notifies PLC sync. History lives in <see cref="RecipeHistory"/>, input checks in
/// <see cref="StepMutationValidator"/>.
/// </summary>
public sealed class RecipeSession(
	RecipeAnalyzer analyzer,
	RecipeMetadataRegistry recipeMetadataRegistry,
	FormulaEvaluator formulaEvaluator,
	ImportedRecipeValidator importedRecipeValidator,
	IPlcSyncService syncService,
	ILogger<RecipeSession> logger)
{
	private readonly RecipeAnalyzer _analyzer = analyzer;
	private readonly FormulaEvaluator _formulaEvaluator = formulaEvaluator;
	private readonly RecipeHistory _history = new();
	private readonly ImportedRecipeValidator _importedRecipeValidator = importedRecipeValidator;
	private readonly ILogger<RecipeSession> _logger = logger;
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry = recipeMetadataRegistry;
	private readonly StepMutationValidator _stepMutationValidator = new(recipeMetadataRegistry);
	private readonly IPlcSyncService _syncService = syncService;

	public Recipe Current => Snapshot.IsSuccess
		? Snapshot.Value.Recipe
		: Recipe.Empty;

	public Recipe LastValidRecipe { get; private set; } = Recipe.Empty;

	public bool IsDirty { get; private set; }

	public bool IsValid =>
		Snapshot.IsSuccess && !Snapshot.Reasons.OfType<Warning>().Any();

	public Result<RecipeSnapshot> Snapshot { get; private set; } = Result.Ok(RecipeSnapshot.Empty);

	public bool CanUndo => _history.UndoCount > 0;

	internal int UndoCount => _history.UndoCount;

	public bool CanRedo => _history.CanRedo;

	public Result Apply(Recipe newRecipe)
	{
		var snapshot = _analyzer.Analyze(newRecipe);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		_history.Push(Current);
		UpdateSnapshot(snapshot);
		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Undo()
	{
		var previous = _history.PeekUndo();
		if (previous is null)
		{
			_logger.LogInformation("Undo requested but no state available");
			return Result.Fail(new NoStateToUndoError());
		}

		var result = RestoreFromHistory(previous, _history.CommitUndo);
		if (result.IsSuccess)
		{
			_logger.LogInformation("Undo applied: restored recipe with {StepCount} steps", Current.StepCount);
		}

		return result;
	}

	public Result Redo()
	{
		var next = _history.PeekRedo();
		if (next is null)
		{
			_logger.LogInformation("Redo requested but no state available");
			return Result.Fail(new NoStateToRedoError());
		}

		var result = RestoreFromHistory(next, _history.CommitRedo);
		if (result.IsSuccess)
		{
			_logger.LogInformation("Redo applied: restored recipe with {StepCount} steps", Current.StepCount);
		}

		return result;
	}

	public Result Reset()
	{
		_history.Clear();
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
		IsDirty = false;
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
		_history.Clear();
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
	/// Validates an untrusted recipe (CSV file, PLC read, reconnect reconciliation) and loads it only when
	/// validation passes; on failure no state changes and the validation <see cref="Result"/> is returned.
	/// </summary>
	public Result LoadAsCurrentValidated(Recipe recipe)
	{
		var validation = _importedRecipeValidator.Validate(recipe);
		return validation.IsFailed ? validation : LoadAsCurrent(recipe);
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

		return ApplyWithOutcome(Current.AppendStep(step), stepCount => stepCount - 1);
	}

	public Result<MutationOutcome> InsertStep(int index, int actionId)
	{
		_logger.LogInformation(
			"Mutation entry: InsertStep StepIndex={StepIndex}, ActionId={ActionId}, StepCount={StepCount}",
			index,
			actionId,
			Current.StepCount);

		var indexCheck = StepMutationValidator.CheckInsertIndex(Current, index);
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

		return ApplyWithOutcome(Current.InsertStep(index, step), _ => index);
	}

	public Result<MutationOutcome> RemoveStep(int index)
	{
		_logger.LogInformation(
			"Mutation entry: RemoveStep StepIndex={StepIndex}, StepCount={StepCount}",
			index,
			Current.StepCount);

		var indexCheck = StepMutationValidator.CheckStepIndex(Current, index);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		return ApplyWithOutcome(
			Current.RemoveStep(index),
			stepCount => stepCount > 0 ? Math.Min(index, stepCount - 1) : null);
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
			var indexCheck = StepMutationValidator.CheckStepIndex(current, i);
			if (indexCheck.IsFailed)
			{
				return indexCheck.ToResult<MutationOutcome>();
			}
		}

		return ApplyWithOutcome(
			current.RemoveSteps(distinctIndices),
			stepCount => stepCount > 0 ? Math.Min(distinctIndices.Min(), stepCount - 1) : null);
	}

	public Result<MutationOutcome> InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		_logger.LogInformation(
			"Mutation entry: InsertSteps StartIndex={StartIndex}, Count={Count}, StepCount={StepCount}",
			startIndex,
			steps.Count,
			Current.StepCount);

		var indexCheck = StepMutationValidator.CheckInsertIndex(Current, startIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck.ToResult<MutationOutcome>();
		}

		return ApplyWithOutcome(Current.InsertSteps(startIndex, steps), _ => startIndex);
	}

	public Result<MutationOutcome> ChangeStepAction(int stepIndex, int newActionId)
	{
		_logger.LogInformation(
			"Mutation entry: ChangeStepAction StepIndex={StepIndex}, ActionId={ActionId}, StepCount={StepCount}",
			stepIndex,
			newActionId,
			Current.StepCount);

		var indexCheck = StepMutationValidator.CheckStepIndex(Current, stepIndex);
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

		return ApplyWithOutcome(Current.ReplaceStep(stepIndex, step), _ => stepIndex);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		_logger.LogInformation(
			"Mutation entry: UpdateStepProperty StepIndex={StepIndex}, ColumnKey={ColumnKey}, StepCount={StepCount}",
			stepIndex,
			columnKey,
			Current.StepCount);

		var current = Current;
		var actionResult = _stepMutationValidator.ResolveStepAction(current, stepIndex);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var action = actionResult.Value;
		var step = current.Steps[stepIndex];

		var valueResult = _stepMutationValidator.ParseColumnValue(action, columnKey, value);
		if (valueResult.IsFailed)
		{
			return valueResult.ToResult();
		}

		var updatedStep = step.WithProperty(columnKey, valueResult.Value);

		var recalcResult = TryApplyFormulaRecalc(updatedStep, action, columnKey, stepIndex);
		if (recalcResult.IsFailed)
		{
			return recalcResult.ToResult();
		}

		updatedStep = recalcResult.Value;

		var newRecipe = current.ReplaceStep(stepIndex, updatedStep);

		return Apply(newRecipe);
	}

	/// <summary>
	/// Applies a selector-column edit as a single undo unit: sets the selector value, drops the
	/// values of columns that the new selection deactivates, and seeds default values for columns
	/// the new selection activates. The whole composition change is committed through a single
	/// <see cref="Apply"/> call, so one <see cref="Undo"/> restores both the prior selector value
	/// and the prior dropped values together.
	/// </summary>
	public Result UpdateStepForSelectorChange(
		int stepIndex,
		string selectorKey,
		string value,
		IReadOnlyCollection<string> columnsToDrop,
		IReadOnlyCollection<string> columnsToSeed)
	{
		_logger.LogInformation(
			"Mutation entry: UpdateStepForSelectorChange StepIndex={StepIndex}, SelectorKey={SelectorKey}, "
			+ "DropCount={DropCount}, SeedCount={SeedCount}, StepCount={StepCount}",
			stepIndex,
			selectorKey,
			columnsToDrop.Count,
			columnsToSeed.Count,
			Current.StepCount);

		var current = Current;
		var actionResult = _stepMutationValidator.ResolveStepAction(current, stepIndex);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var action = actionResult.Value;
		var step = current.Steps[stepIndex];

		var selectorResult = _stepMutationValidator.ParseColumnValue(action, selectorKey, value);
		if (selectorResult.IsFailed)
		{
			return selectorResult.ToResult();
		}

		var properties = step.Properties.SetItem(new PropertyId(selectorKey), selectorResult.Value);

		foreach (var dropKey in columnsToDrop)
		{
			properties = properties.Remove(new PropertyId(dropKey));
		}

		// Resolve seed defaults through StepInitializer so a newly-activated column with no
		// default_value still gets a valid default instead of an empty string that fails validation.
		foreach (var seedKey in columnsToSeed)
		{
			var seedPropertyResult = action.FindProperty(seedKey);
			if (seedPropertyResult.IsFailed)
			{
				return seedPropertyResult.ToResult();
			}

			var seedValue = StepInitializer.ResolveDefaultValue(seedPropertyResult.Value, _recipeMetadataRegistry);
			properties = properties.SetItem(new PropertyId(seedKey), seedValue);
		}

		var updatedStep = step with { Properties = properties };

		var recalcResult = TryApplyFormulaRecalc(updatedStep, action, selectorKey, stepIndex);
		if (recalcResult.IsFailed)
		{
			return recalcResult.ToResult();
		}

		updatedStep = recalcResult.Value;

		var newRecipe = current.ReplaceStep(stepIndex, updatedStep);

		return Apply(newRecipe);
	}

	private Result<MutationOutcome> ApplyWithOutcome(Recipe newRecipe, Func<int, int?> selectionForStepCount)
	{
		var applyResult = Apply(newRecipe);
		return applyResult.IsFailed
			? applyResult.ToResult<MutationOutcome>()
			: Result.Ok(new MutationOutcome(selectionForStepCount(Current.StepCount)))
				.WithReasons(applyResult.Reasons);
	}

	private Result<Step> TryApplyFormulaRecalc(Step step, ActionDefinition action, string columnKey, int stepIndex)
	{
		if (action.Formula is null
			|| !action.Formula.RecalcOrder.Contains(columnKey, StringComparer.OrdinalIgnoreCase))
		{
			return Result.Ok(step);
		}

		var activeColumns = ActiveColumnSetResolver.Resolve(action, step);
		var recalcResult = _formulaEvaluator.Recalculate(step, action, columnKey, activeColumns);
		if (recalcResult.IsFailed)
		{
			_logger.LogInformation(
				"Formula recalculation rejected edit on StepIndex={StepIndex}, ColumnKey={ColumnKey}: {Errors}",
				stepIndex,
				columnKey,
				string.Join("; ", recalcResult.Errors.Select(e => e.Message)));
		}

		return recalcResult;
	}

	private void UpdateSnapshot(Result<RecipeSnapshot> snapshot)
	{
		UpdateSnapshot(snapshot, markDirty: true);
	}

	private void UpdateSnapshot(Result<RecipeSnapshot> snapshot, bool markDirty)
	{
		Snapshot = snapshot;

		if (markDirty)
		{
			IsDirty = true;
		}

		if (snapshot.IsSuccess)
		{
			LastValidRecipe = snapshot.Value.Recipe;
		}
	}

	private void ResetSnapshotToEmpty()
	{
		Snapshot = RecipeSnapshot.Empty;
		LastValidRecipe = Recipe.Empty;
		IsDirty = false;
	}

	private Result RestoreFromHistory(Recipe target, Action<Recipe> commitMove)
	{
		var snapshot = _analyzer.Analyze(target);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		commitMove(Current);
		UpdateSnapshot(snapshot);
		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	private void NotifySyncIfEnabled()
	{
		if (_syncService.IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(Current, IsValid);
		}
	}
}
