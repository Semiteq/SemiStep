using FluentResults;

using SemiStep.Core.Plc;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.State;

using Serilog;

namespace SemiStep.Core.Recipes;

public sealed class RecipeWorkspace
{
	private readonly RecipeAnalyzer _analyzer;
	private readonly RecipeHistoryManager _historyManager;
	private readonly RecipeStateManager _stateManager;
	private readonly IPlcSyncService _syncService;

	internal RecipeWorkspace(
		RecipeStateManager stateManager,
		RecipeHistoryManager historyManager,
		RecipeAnalyzer analyzer,
		IPlcSyncService syncService)
	{
		_stateManager = stateManager;
		_historyManager = historyManager;
		_analyzer = analyzer;
		_syncService = syncService;
	}

	public Recipe CurrentRecipe => _stateManager.Current;
	public Recipe LastValidRecipe => _stateManager.LastValidRecipe;
	public bool IsDirty => _stateManager.IsDirty;
	public bool IsValid => _stateManager.IsValid;
	public Result<RecipeSnapshot> Snapshot => _stateManager.LatestSnapshot ?? RecipeSnapshot.Empty;

	public bool CanUndo => _historyManager.CanUndo;
	public bool CanRedo => _historyManager.CanRedo;

	public Result Apply(Recipe newRecipe)
	{
		var snapshot = _analyzer.Analyze(newRecipe);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		_historyManager.Push(_stateManager.Current);
		_stateManager.Update(snapshot);
		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Undo()
	{
		var previous = _historyManager.Undo(_stateManager.Current);
		if (previous is null)
		{
			return Result.Fail("No state to undo to");
		}

		var snapshot = _analyzer.Analyze(previous);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Redo()
	{
		var next = _historyManager.Redo(_stateManager.Current);
		if (next is null)
		{
			return Result.Fail("No state to redo to");
		}

		var snapshot = _analyzer.Analyze(next);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		NotifySyncIfEnabled();

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result Reset()
	{
		_historyManager.Clear();
		_stateManager.Reset();

		var snapshot = _analyzer.Analyze(Recipe.Empty);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			Log.Warning("Empty recipe analysis unexpectedly failed: {Errors}",
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
		}

		NotifySyncIfEnabled();

		return snapshot.ToResult();
	}

	public void MarkSaved()
	{
		_stateManager.MarkSaved();
	}

	/// <summary>
	/// Replaces the current recipe with <paramref name="recipe"/> as a fresh editing session.
	/// Differs from <see cref="Apply"/>: clears undo history (does not push the previous state)
	/// because the new recipe represents a load (from CSV file or PLC), not an incremental edit.
	/// Differs from <see cref="Reset"/>: loads a specific recipe instead of <see cref="Recipe.Empty"/>.
	/// </summary>
	public Result LoadAsCurrent(Recipe recipe)
	{
		_historyManager.Clear();
		var snapshot = _analyzer.Analyze(recipe);
		_stateManager.Update(snapshot);
		NotifySyncIfEnabled();

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

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

	private void NotifySyncIfEnabled()
	{
		if (_syncService.IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}
	}
}
