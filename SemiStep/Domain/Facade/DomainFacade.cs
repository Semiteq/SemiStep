using System.Collections.Immutable;

using Domain.Services;
using Domain.State;

using FluentResults;

using Serilog;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.Core;
using Shared.Plc;
using Shared.Results;
using Shared.ServiceContracts;

namespace Domain.Facade;

public sealed class DomainFacade : IDisposable
{
	private readonly IActionRegistry _actionRegistry;
	private readonly AppConfiguration _appConfiguration;
	private readonly ICsvClipboardService _clipboardService;
	private readonly IColumnRegistry _columnRegistry;
	private readonly IS7ConnectionService _connectionService;
	private readonly CoreService _coreService;
	private readonly ICsvService _csvService;
	private readonly IGroupRegistry _groupRegistry;
	private readonly RecipeHistoryManager _historyManager;
	private readonly IPropertyRegistry _propertyRegistry;
	private readonly RecipeStateManager _stateManager;
	private Action<PlcConnectionState>? _connectionStateChangedRelay;

	private bool _disposed;
	private Action<Recipe>? _recipeChangedRelay;

	internal DomainFacade(
		AppConfiguration appConfiguration,
		IActionRegistry actionRegistry,
		IPropertyRegistry propertyRegistry,
		IColumnRegistry columnRegistry,
		IGroupRegistry groupRegistry,
		CoreService coreService,
		RecipeStateManager stateManager,
		RecipeHistoryManager historyManager,
		ICsvService csvService,
		IS7ConnectionService connectionService,
		ICsvClipboardService clipboardService)
	{
		_appConfiguration = appConfiguration;
		_actionRegistry = actionRegistry;
		_propertyRegistry = propertyRegistry;
		_columnRegistry = columnRegistry;
		_groupRegistry = groupRegistry;
		_coreService = coreService;
		_stateManager = stateManager;
		_historyManager = historyManager;
		_csvService = csvService;
		_connectionService = connectionService;
		_clipboardService = clipboardService;
	}

	public Recipe CurrentRecipe => _stateManager.Current;
	public Recipe LastValidRecipe => _stateManager.LastValidRecipe;

	public bool IsDirty => _stateManager.IsDirty;
	public bool IsValid => _stateManager.IsValid;
	public RecipeSnapshot Snapshot => _stateManager.LastSnapshot ?? RecipeSnapshot.Empty;

	public bool CanUndo => _historyManager.CanUndo;
	public bool CanRedo => _historyManager.CanRedo;

	public bool IsConnected => _connectionService.IsConnected;
	public string? LastConnectionError { get; private set; }

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_recipeChangedRelay is not null)
		{
			_stateManager.RecipeChanged -= _recipeChangedRelay;
		}

		if (_connectionStateChangedRelay is not null)
		{
			_connectionService.StateChanged -= _connectionStateChangedRelay;
		}
	}

	public event Action<Recipe>? RecipeChanged;

	public static CellState GetCellState(GridColumnDefinition column, ActionDefinition action)
	{
		return CellStateResolver.GetCellState(column, action);
	}

	public event Action? ConnectionStateChanged;

	public void Initialize()
	{
		_actionRegistry.Initialize(_appConfiguration.Actions);
		_propertyRegistry.Initialize(_appConfiguration.Properties);
		_columnRegistry.Initialize(_appConfiguration.Columns);
		_groupRegistry.Initialize(_appConfiguration.Groups);

		_coreService.NewRecipe();

		_recipeChangedRelay = recipe => RecipeChanged?.Invoke(recipe);
		_stateManager.RecipeChanged += _recipeChangedRelay;

		_connectionStateChangedRelay = _ => ConnectionStateChanged?.Invoke();
		_connectionService.StateChanged += _connectionStateChangedRelay;
		StartPlcConnection(_appConfiguration.PlcConfiguration);
	}

	public void NewRecipe()
	{
		_historyManager.Clear();
		_coreService.NewRecipe();
	}

	public void InsertStep(int index, int actionId)
	{
		_historyManager.Push(_stateManager.Current);
		var snapshot = _coreService.InsertStep(index, actionId);
		_stateManager.Update(snapshot);
	}

	public void AppendStep(int actionId)
	{
		_historyManager.Push(_stateManager.Current);
		var snapshot = _coreService.AppendStep(actionId);
		_stateManager.Update(snapshot);
	}

	public void ChangeStepAction(int stepIndex, int newActionId)
	{
		_historyManager.Push(_stateManager.Current);
		var snapshot = _coreService.ChangeStepAction(stepIndex, newActionId);
		_stateManager.Update(snapshot);
	}

	public void RemoveStep(int index)
	{
		var snapshot = _coreService.RemoveStep(index);

		HistoryPushOnlyValidState(snapshot);
		_stateManager.Update(snapshot);
	}

	public void InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		_historyManager.Push(_stateManager.Current);
		var snapshot = _coreService.InsertSteps(startIndex, steps);
		_stateManager.Update(snapshot);
	}

	public void RemoveSteps(IReadOnlyList<int> indices)
	{
		var snapshot = _coreService.RemoveSteps(indices);

		HistoryPushOnlyValidState(snapshot);
		_stateManager.Update(snapshot);
	}

	public void UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		var snapshot = _coreService.UpdateStepProperty(stepIndex, columnKey, value);

		HistoryPushOnlyValidState(snapshot);
		_stateManager.Update(snapshot);
	}

	public RecipeSnapshot? Undo()
	{
		var previous = _historyManager.Undo(_stateManager.Current);
		if (previous is null)
		{
			return null;
		}

		var snapshot = _coreService.AnalyzeRecipe(previous);
		_stateManager.Update(snapshot);

		return snapshot;
	}

	public RecipeSnapshot? Redo()
	{
		var next = _historyManager.Redo(_stateManager.Current);
		if (next is null)
		{
			return null;
		}

		var snapshot = _coreService.AnalyzeRecipe(next);
		_stateManager.Update(snapshot);

		return snapshot;
	}

	public async Task<Result<Recipe>> LoadRecipeAsync(string filePath, CancellationToken ct = default)
	{
		var result = await _csvService.LoadAsync(filePath, ct);
		if (result.IsFailed)
		{
			return result;
		}

		_historyManager.Clear();
		var snapshot = _coreService.AnalyzeRecipe(result.Value);
		_stateManager.Update(snapshot);
		_stateManager.MarkSaved();

		return result;
	}

	public async Task SaveRecipeAsync(string filePath, CancellationToken ct = default)
	{
		await _csvService.SaveAsync(_stateManager.Current, filePath, ct);
		_stateManager.MarkSaved();
	}

	public string SerializeStepsForClipboard(IReadOnlyList<Step> steps)
	{
		var recipe = new Recipe(steps.ToImmutableList());

		return _clipboardService.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeStepsFromClipboard(string csvBody)
	{
		return _clipboardService.DeserializeSteps(csvBody);
	}

	public void MarkSaved()
	{
		_stateManager.MarkSaved();
	}

	public void StartPlcConnection(
		PlcConfiguration plcConfiguration)
	{
		if (IsConnected)
		{
			Log.Warning("PLC connection is already established");

			return;
		}

		Task.Run(async () =>
		{
			try
			{
				LastConnectionError = null;
				await _connectionService.ConnectAsync(plcConfiguration.Connection);
			}
			catch (Exception ex)
			{
				LastConnectionError = ex.Message;
				Log.Warning(ex, "Initial PLC connection failed, auto-reconnect will retry");
			}
		});
	}

	public void StopPlcConnection()
	{
		if (!IsConnected)
		{
			Log.Warning("PLC connection is not established");

			return;
		}

		Task.Run(async () =>
		{
			try
			{
				await _connectionService.DisconnectAsync();
				await _connectionService.DisposeAsync();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Error while disconnecting PLC");
			}
		});
	}

	private void HistoryPushOnlyValidState(RecipeSnapshot snapshot)
	{
		if (snapshot.IsValid)
		{
			_historyManager.Push(_stateManager.Current);
		}
	}
}
