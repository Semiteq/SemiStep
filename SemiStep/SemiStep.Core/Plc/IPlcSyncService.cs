using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc;

public interface IPlcSyncService
{
	void NotifyRecipeChanged(Recipe recipe, bool isValid);

	void Reset();

	void SetSyncEnabled(bool value);

	void UpdateConnectionState(PlcConnectionState state);

	bool IsSyncEnabled { get; }

	PlcSyncStatus Status { get; }

	DateTimeOffset? LastSyncTime { get; }

	IObservable<Result<PlcSessionSnapshot>> PlcState { get; }
}
