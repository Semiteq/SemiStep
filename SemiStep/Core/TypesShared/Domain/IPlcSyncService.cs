using FluentResults;

using TypesShared.Core;
using TypesShared.Plc;

namespace TypesShared.Domain;

public interface IPlcSyncService
{
	void NotifyRecipeChanged(Recipe recipe, bool isValid);

	void Reset();

	void SetSyncEnabled(bool value);

	void UpdateConnectionState(PlcConnectionState state);

	PlcSyncStatus Status { get; }

	DateTimeOffset? LastSyncTime { get; }

	IObservable<Result<PlcSessionSnapshot>> PlcState { get; }
}
