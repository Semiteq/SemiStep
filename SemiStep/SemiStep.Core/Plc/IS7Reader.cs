using FluentResults;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc;

public interface IS7Reader
{
	Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync();
	Task<Result<Recipe>> ReadRecipeFromPlcAsync();
}
