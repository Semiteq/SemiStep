using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

namespace SemiStep.Tests.UI.RecipeGrid;

public sealed class CanonicalRecipeGridSurfaceContractTests : RecipeGridSurfaceContractTests
{
	protected override IRecipeGridSurface CreateSurface(UIFixture fixture)
	{
		return fixture.CreateCanonicalSurface();
	}

	protected override RecipeRowViewModel RowAt(int index)
	{
		return ((CanonicalRecipeGridSurface)Surface).RecipeRows[index];
	}
}
