using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc;

public interface IPlcSyncService
{
	void NotifyRecipeChanged(Recipe recipe, bool isValid);

	void ResetForDisable();

	void HandleConnectionLost();

	/// <summary>
	/// Completes when the attempt in flight at the call has ended; throws when <paramref name="ct"/> cancels first.
	/// </summary>
	Task WaitForPendingSyncAsync(CancellationToken ct = default);

	void SetSyncEnabled(bool value);

	void UpdateConnectionState(PlcConnectionState state);

	bool IsSyncEnabled { get; }

	PlcSyncStatus Status { get; }

	DateTimeOffset? LastSyncTime { get; }

	IObservable<PlcSessionSnapshot> PlcState { get; }

	IObservable<IError> Faults { get; }
}
