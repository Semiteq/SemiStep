using System.Reactive.Subjects;

using FluentResults;

using SemiStep.Core.Plc;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

public sealed class StubPlcSyncService : IPlcSyncService
{
	private readonly BehaviorSubject<Result<PlcSessionSnapshot>> _plcStateSubject = new(
		PlcSessionSnapshot.InitialState);

	public bool IsSyncEnabled { get; private set; }

	public PlcSyncStatus Status => PlcSyncStatus.Idle;

	public DateTimeOffset? LastSyncTime => null;

	public IObservable<Result<PlcSessionSnapshot>> PlcState => _plcStateSubject;

	/// <summary>True if <see cref="ResetForDisable"/> was called at least once.</summary>
	public bool WasResetForDisableCalled { get; private set; }

	/// <summary>True if <see cref="HandleConnectionLost"/> was called at least once.</summary>
	public bool WasHandleConnectionLostCalled { get; private set; }

	/// <summary>Number of times <see cref="NotifyRecipeChanged"/> was called.</summary>
	public int NotifyRecipeChangedCallCount { get; private set; }

	/// <summary>Calls recorded by <see cref="NotifyRecipeChanged"/>, in order.</summary>
	public List<(Recipe Recipe, bool IsValid)> NotifyRecipeChangedCalls { get; } = new();

	/// <summary>Pushes a new PLC state snapshot to subscribers of <see cref="PlcState"/>.</summary>
	public void PushPlcState(Result<PlcSessionSnapshot> state)
	{
		_plcStateSubject.OnNext(state);
	}

	public void NotifyRecipeChanged(Recipe recipe, bool isValid)
	{
		NotifyRecipeChangedCallCount++;
		NotifyRecipeChangedCalls.Add((recipe, isValid));
	}

	public void ResetForDisable()
	{
		WasResetForDisableCalled = true;
	}

	public void HandleConnectionLost()
	{
		WasHandleConnectionLostCalled = true;
	}

	public void SetSyncEnabled(bool value)
	{
		IsSyncEnabled = value;
	}

	public void UpdateConnectionState(PlcConnectionState state)
	{
	}
}
