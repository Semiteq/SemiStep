using System.Reactive.Subjects;

using Domain.Plc;

using FluentResults;

using TypesShared.Core;
using TypesShared.Plc;

namespace Tests.Helpers;

public sealed class StubPlcSyncService : IPlcSyncService
{
	private readonly BehaviorSubject<Result<PlcSessionSnapshot>> _plcStateSubject = new(
		PlcSessionSnapshot.InitialState);

	public PlcSyncStatus Status => PlcSyncStatus.Idle;

	public DateTimeOffset? LastSyncTime => null;

	public IObservable<Result<PlcSessionSnapshot>> PlcState => _plcStateSubject;

	/// <summary>True if <see cref="Reset"/> was called at least once.</summary>
	public bool WasResetCalled { get; private set; }

	/// <summary>Number of times <see cref="NotifyRecipeChanged"/> was called.</summary>
	public int NotifyRecipeChangedCallCount { get; private set; }

	/// <summary>Pushes a new PLC state snapshot to subscribers of <see cref="PlcState"/>.</summary>
	public void PushPlcState(Result<PlcSessionSnapshot> state)
	{
		_plcStateSubject.OnNext(state);
	}

	public void NotifyRecipeChanged(Recipe recipe, bool isValid)
	{
		NotifyRecipeChangedCallCount++;
	}

	public void Reset()
	{
		WasResetCalled = true;
	}

	public void SetSyncEnabled(bool value)
	{
	}

	public void UpdateConnectionState(PlcConnectionState state)
	{
	}
}
