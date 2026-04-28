using Core.Analysis;

using Domain.Plc;
using Domain.State;

using FluentResults;

using Serilog;

using TypesShared.Core;
using TypesShared.Results;

namespace Domain;

public sealed class RecipeWorkspace
{
	private readonly RecipeAnalyzer _analyzer;
	private readonly RecipeHistoryManager _historyManager;
	private readonly RecipeStateManager _stateManager;
	private readonly IPlcSyncService _syncService;
	private Func<bool> _syncEnabledProvider = () => false;

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

	internal void SetSyncEnabledProvider(Func<bool> provider)
	{
		_syncEnabledProvider = provider;
	}

	public Result Apply(Recipe newRecipe)
	{
		var snapshot = _analyzer.Analyze(newRecipe);
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		_historyManager.Push(_stateManager.Current);
		_stateManager.Update(snapshot);

		if (_syncEnabledProvider())
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

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

		if (_syncEnabledProvider())
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

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

		if (_syncEnabledProvider())
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

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

		if (_syncEnabledProvider())
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

		return snapshot.ToResult();
	}

	public void MarkSaved()
	{
		_stateManager.MarkSaved();
	}

	public Result LoadAsCurrent(Recipe recipe)
	{
		_historyManager.Clear();
		var snapshot = _analyzer.Analyze(recipe);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		if (_syncEnabledProvider())
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

		return Result.Ok().WithReasons(snapshot.Reasons);
	}
}
