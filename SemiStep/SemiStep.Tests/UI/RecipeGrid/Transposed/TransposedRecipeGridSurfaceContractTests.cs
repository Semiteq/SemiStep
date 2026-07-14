using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

public sealed class TransposedRecipeGridSurfaceContractTests : RecipeGridSurfaceContractTests
{
	protected override IRecipeGridSurface CreateSurface(UIFixture fixture)
	{
		return fixture.CreateTransposedSurface();
	}

	protected override RecipeRowViewModel RowAt(int index)
	{
		return ((TransposedRecipeGridSurface)Surface).StepColumns[index].Row;
	}
}
