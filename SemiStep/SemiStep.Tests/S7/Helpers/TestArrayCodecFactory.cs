using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Serialization;

namespace SemiStep.Tests.S7.Helpers;

/// <summary>
/// Builds an <see cref="ArrayCodec"/> for tests using a fixed WString max-chars value
/// so callers do not have to wire up a full <see cref="SemiStep.Core.Recipes.RecipeMetadataRegistry"/>.
/// </summary>
internal static class TestArrayCodecFactory
{
	public const int DefaultWStringMaxChars = 32;

	public static ArrayCodec Create(PlcConfiguration configuration, int wStringMaxChars = DefaultWStringMaxChars)
	{
		var layout = configuration.Layout;
		return new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, wStringMaxChars);
	}
}
