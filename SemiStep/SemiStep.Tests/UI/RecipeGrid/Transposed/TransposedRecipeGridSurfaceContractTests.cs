using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

public sealed class TransposedRecipeGridSurfaceContractTests : RecipeGridSurfaceContractTests
{
	protected override IRecipeGridSurface CreateSurface(UIFixture fixture)
	{
		return fixture.CreateTransposedSurface();
	}
}
