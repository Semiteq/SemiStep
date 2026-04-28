using System.Collections.Immutable;

using Domain.Helpers;
using Domain.State;

using FluentResults;

using Serilog;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

namespace Domain.Facade;

public sealed class DomainFacade : IDisposable
{
	private readonly IClipboardService _clipboardService;
	private readonly IS7Service _connectionService;
	private readonly ICoreService _coreService;
	private readonly ICsvService _csvService;
	private readonly RecipeHistoryManager _historyManager;
	private readonly ImportedRecipeValidator _importedRecipeValidator;
	private readonly RecipeStateManager _stateManager;
	private readonly IPlcSyncService _syncService;
	private readonly PlcLifecycleManager _plcLifecycleManager;
	private readonly RecipeEditService _editService;
	private bool _disposed;

	internal DomainFacade(
		ConfigRegistry configRegistry,
		ICoreService coreService,
		RecipeStateManager stateManager,
		RecipeHistoryManager historyManager,
		ICsvService csvService,
		IS7Service connectionService,
		IClipboardService clipboardService,
		ImportedRecipeValidator importedRecipeValidator,
		IPropertyParser propertyParser,
		IPlcSyncService syncService)
	{
		_coreService = coreService;
		_stateManager = stateManager;
		_historyManager = historyManager;
		_csvService = csvService;
		_connectionService = connectionService;
		_clipboardService = clipboardService;
		_importedRecipeValidator = importedRecipeValidator;
		_syncService = syncService;
		_plcLifecycleManager = new PlcLifecycleManager(
			connectionService,
			coreService,
			historyManager,
			stateManager,
			syncService,
			(local, plc) => PlcRecipeConflictDetected?.Invoke(local, plc));
		_editService = new RecipeEditService(
			coreService,
			stateManager,
			historyManager,
			propertyParser,
			configRegistry,
			syncService,
			() => _plcLifecycleManager.IsSyncEnabled);
	}

	public Recipe CurrentRecipe => _stateManager.Current;
	public Recipe LastValidRecipe => _stateManager.LastValidRecipe;
	public bool IsDirty => _stateManager.IsDirty;
	public bool IsValid => _stateManager.IsValid;
	public Result<RecipeSnapshot> Snapshot => _stateManager.LatestSnapshot ?? RecipeSnapshot.Empty;

	public bool CanUndo => _historyManager.CanUndo;
	public bool CanRedo => _historyManager.CanRedo;

	public bool IsSyncEnabled => _plcLifecycleManager.IsSyncEnabled;
	public bool IsConnected => _connectionService.IsConnected;
	public bool IsRecipeActive => _connectionService.IsRecipeActive;
	public IObservable<PlcExecutionInfo> ExecutionState => _connectionService.ExecutionState;

	public PlcSyncStatus SyncStatus => _syncService.Status;
	public DateTimeOffset? LastSyncTime => _syncService.LastSyncTime;

	public IObservable<Result<PlcSessionSnapshot>> PlcState => _syncService.PlcState;

	public event Action<Recipe, Recipe>? PlcRecipeConflictDetected;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_plcLifecycleManager.Dispose();
	}

	public static CellState GetCellState(GridColumnDefinition column, ActionDefinition action)
	{
		return CellStateResolver.GetCellState(column, action);
	}

	public void Initialize()
	{
		_ = SetNewRecipe();
		_plcLifecycleManager.Initialize();
	}

	public Result AppendStep(int actionId)
	{
		return _editService.AppendStep(actionId);
	}

	public Result InsertStep(int index, int actionId)
	{
		return _editService.InsertStep(index, actionId);
	}

	public Result RemoveStep(int index)
	{
		return _editService.RemoveStep(index);
	}

	public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		return _editService.InsertSteps(startIndex, steps);
	}

	public Result RemoveSteps(IReadOnlyList<int> indices)
	{
		return _editService.RemoveSteps(indices);
	}

	public Result ChangeStepAction(int stepIndex, int newActionId)
	{
		return _editService.ChangeStepAction(stepIndex, newActionId);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		return _editService.UpdateStepProperty(stepIndex, columnKey, value);
	}

	public Result Undo()
	{
		var previous = _historyManager.Undo(_stateManager.Current);
		if (previous is null)
		{
			return Result.Fail("No state to undo to");
		}

		var snapshot = _coreService.AnalyzeRecipe(previous);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		if (IsSyncEnabled)
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

		var snapshot = _coreService.AnalyzeRecipe(next);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		if (IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public async Task<Result> LoadRecipeAsync(string filePath)
	{
		var loadResult = await _csvService.LoadAsync(filePath);
		if (loadResult.IsFailed)
		{
			return loadResult.ToResult();
		}

		var validationResult = _importedRecipeValidator.Validate(loadResult.Value);
		if (validationResult.IsFailed)
		{
			return validationResult;
		}

		_historyManager.Clear();
		var snapshot = _coreService.AnalyzeRecipe(loadResult.Value);
		_stateManager.Update(snapshot);
		_stateManager.MarkSaved();

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		if (IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public async Task<Result> SaveRecipeAsync(string filePath)
	{
		try
		{
			await _csvService.SaveAsync(_stateManager.Current, filePath);
			_stateManager.MarkSaved();

			return Result.Ok();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save recipe to {FilePath}", filePath);

			return Result.Fail(ex.Message);
		}
	}

	public void MarkSaved()
	{
		_stateManager.MarkSaved();
	}

	public string SerializeStepsForClipboard(IReadOnlyList<Step> steps)
	{
		var recipe = new Recipe(steps.ToImmutableList());

		return _clipboardService.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeStepsFromClipboard(string csvBody)
	{
		var result = _clipboardService.DeserializeSteps(csvBody);
		if (result.IsFailed)
		{
			return result;
		}

		var validationResult = _importedRecipeValidator.Validate(result.Value);
		if (validationResult.IsFailed)
		{
			return validationResult.ToResult<Recipe>();
		}

		return result;
	}

	public async Task<Result> EnableSync(PlcConfiguration config)
	{
		return await _plcLifecycleManager.EnableSync(config);
	}

	public async Task DisableSync()
	{
		await _plcLifecycleManager.DisableSync();
	}

	public async Task<Result> LoadRecipeFromPlcAsync()
	{
		var loadResult = await _connectionService.ReadRecipeFromPlcAsync();
		if (loadResult.IsFailed)
		{
			return loadResult.ToResult();
		}

		var validationResult = _importedRecipeValidator.Validate(loadResult.Value);
		if (validationResult.IsFailed)
		{
			return validationResult;
		}

		_historyManager.Clear();
		var snapshot = _coreService.AnalyzeRecipe(loadResult.Value);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		if (IsSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	public Result ResolveConflict(bool keepLocal)
	{
		return _plcLifecycleManager.ResolveConflict(keepLocal, _stateManager);
	}

	public Result SetNewRecipe()
	{
		_historyManager.Clear();
		_stateManager.Reset();

		var snapshot = _coreService.AnalyzeRecipe(Recipe.Empty);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			Log.Warning("Empty recipe analysis unexpectedly failed: {Errors}",
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
		}

		return snapshot.ToResult();
	}
}
