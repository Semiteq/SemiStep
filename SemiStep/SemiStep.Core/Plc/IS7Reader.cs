using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc;

public interface IS7Reader
{
	Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync(CancellationToken ct = default);
	Task<Result<Recipe>> ReadRecipeFromPlcAsync(CancellationToken ct = default);
	Task<Result<int>> ReadProtocolVersionAsync(CancellationToken ct = default);
}
