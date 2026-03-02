using Core.Analysis;
using Core.Entities;

using Domain.Ports;
using Domain.Services;
using Domain.State;

using Serilog;

using Shared;
using Shared.Entities;
using Shared.Registries;

namespace Domain.Facade;

public sealed class DomainFacade(
	AppConfiguration appConfiguration,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry,
	IGroupRegistry groupRegistry,
	CoreService coreService,
	RecipeStateManager stateManager,
	RecipeHistoryManager historyManager,
	ICsvService csvService,
	IS7ConnectionService connectionService)
	: IDisposable
{
	private bool _disposed;
	private Action<PlcConnectionState>? _connectionStateChangedRelay;

	public Recipe CurrentRecipe => stateManager.Current;

	public bool IsDirty => stateManager.IsDirty;
	public bool IsValid => stateManager.IsValid;
	public RecipeSnapshot Snapshot => stateManager.LastSnapshot ?? RecipeSnapshot.Empty;

	public bool CanUndo => historyManager.CanUndo;
	public bool CanRedo => historyManager.CanRedo;

	public bool IsConnected => connectionService.IsConnected;
	public string? LastConnectionError { get; private set; }

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_connectionStateChangedRelay is not null)
		{
			connectionService.StateChanged -= _connectionStateChangedRelay;
		}
	}

	public event Action? ConnectionStateChanged;

	public void Initialize()
	{
		actionRegistry.Initialize(appConfiguration.Actions);
		propertyRegistry.Initialize(appConfiguration.Properties);
		columnRegistry.Initialize(appConfiguration.Columns);
		groupRegistry.Initialize(appConfiguration.Groups);

		coreService.NewRecipe();

		_connectionStateChangedRelay = _ => ConnectionStateChanged?.Invoke();
		connectionService.StateChanged += _connectionStateChangedRelay;
		StartPlcConnection(appConfiguration.PlcConfiguration);
	}

	public void NewRecipe()
	{
		historyManager.Clear();
		coreService.NewRecipe();
	}

	public void InsertStep(int index, int actionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.InsertStep(index, actionId);
		stateManager.Update(snapshot);
	}

	public void AppendStep(int actionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.AppendStep(actionId);
		stateManager.Update(snapshot);
	}

	public void ChangeStepAction(int stepIndex, int newActionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.ChangeStepAction(stepIndex, newActionId);
		stateManager.Update(snapshot);
	}

	public void RemoveStep(int index)
	{
		var snapshot = coreService.RemoveStep(index);

		HistoryPushOnlyValidState(snapshot);
		stateManager.Update(snapshot);
	}

	public void UpdateStepProperty(int stepIndex, string columnKey, object value)
	{
		var snapshot = coreService.UpdateStepProperty(stepIndex, columnKey, value);

		HistoryPushOnlyValidState(snapshot);
		stateManager.Update(snapshot);
	}

	public RecipeSnapshot? Undo()
	{
		var previous = historyManager.Undo(stateManager.Current);
		if (previous is null)
		{
			return null;
		}

		var snapshot = coreService.AnalyzeRecipe(previous);
		stateManager.Update(snapshot);

		return snapshot;
	}

	public RecipeSnapshot? Redo()
	{
		var next = historyManager.Redo(stateManager.Current);
		if (next is null)
		{
			return null;
		}

		var snapshot = coreService.AnalyzeRecipe(next);
		stateManager.Update(snapshot);

		return snapshot;
	}

	public async Task LoadRecipeAsync(string filePath, CancellationToken ct = default)
	{
		var recipe = await csvService.LoadAsync(filePath, ct);
		historyManager.Clear();
		var snapshot = coreService.AnalyzeRecipe(recipe);
		stateManager.Update(snapshot);
		stateManager.MarkSaved();
	}

	public async Task SaveRecipeAsync(string filePath, CancellationToken ct = default)
	{
		await csvService.SaveAsync(stateManager.Current, filePath, ct);
		stateManager.MarkSaved();
	}

	public void MarkSaved()
	{
		stateManager.MarkSaved();
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
				await connectionService.ConnectAsync(plcConfiguration.Connection);
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
				await connectionService.DisconnectAsync();
				await connectionService.DisposeAsync();
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
			historyManager.Push(stateManager.Current);
		}
	}
}
