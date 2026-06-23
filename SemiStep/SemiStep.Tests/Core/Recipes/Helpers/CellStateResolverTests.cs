using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Recipes.Helpers;

[Trait("Component", "Core")]
[Trait("Category", "Unit")]
[Trait("Area", "CellStateResolver")]
public sealed class CellStateResolverTests
{
	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenColumnIsReadOnly()
	{
		var column = BuildColumn(key: "duration", readOnly: true);
		IReadOnlySet<string> activeColumnKeys = new HashSet<string>();

		var result = CellStateResolver.IsInapplicable(column, activeColumnKeys);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenColumnIsActionColumn()
	{
		var column = BuildColumn(key: StepValueParser.ActionColumnKey, readOnly: false);
		IReadOnlySet<string> activeColumnKeys = new HashSet<string>();

		var result = CellStateResolver.IsInapplicable(column, activeColumnKeys);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsInapplicable_ReturnsTrue_WhenColumnIsNotActive()
	{
		var column = BuildColumn(key: "temperature", readOnly: false);
		IReadOnlySet<string> activeColumnKeys = new HashSet<string> { "duration" };

		var result = CellStateResolver.IsInapplicable(column, activeColumnKeys);

		result.Should().BeTrue();
	}

	[Fact]
	public void IsInapplicable_ReturnsFalse_WhenColumnIsActive()
	{
		var column = BuildColumn(key: "temperature", readOnly: false);
		IReadOnlySet<string> activeColumnKeys = new HashSet<string> { "temperature" };

		var result = CellStateResolver.IsInapplicable(column, activeColumnKeys);

		result.Should().BeFalse();
	}

	private static GridColumnDefinition BuildColumn(string key, bool readOnly)
	{
		return new GridColumnDefinition(
			Key: key,
			ColumnType: "text",
			UiName: key,
			PropertyTypeId: "string",
			ReadOnly: readOnly,
			SaveToCsv: true);
	}
}
