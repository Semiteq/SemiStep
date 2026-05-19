using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Tests.Helpers;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// Builds an <see cref="ArrayCodec"/> for tests using a fixed WString max-chars value
/// so callers do not have to wire up a full <see cref="SemiStep.Core.Recipes.RecipeMetadataRegistry"/>.
/// Defaults to the shared <see cref="TestRecipeMetadataRegistryFactory.DefaultStringMaxLength"/> constant
/// to keep the codec WString sizing aligned with the registry's string max_length SoT in tests.
/// </summary>
internal static class TestArrayCodecFactory
{
	public static ArrayCodec Create(
		PlcConfiguration configuration,
		int wStringMaxChars = TestRecipeMetadataRegistryFactory.DefaultStringMaxLength)
	{
		var layout = configuration.Layout;
		return new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, wStringMaxChars);
	}
}
