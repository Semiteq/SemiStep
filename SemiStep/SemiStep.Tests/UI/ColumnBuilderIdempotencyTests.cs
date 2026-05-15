using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.SemiStep.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class ColumnBuilderIdempotencyTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void BuildColumns_CalledTwice_ProducesSameColumnCount()
	{
		var columnBuilder = new ColumnBuilder(GridStyleOptions.Default, _fixture.RecipeMetadataRegistry);
		var grid = new DataGrid();

		columnBuilder.BuildColumns(grid);
		var firstCount = grid.Columns.Count;

		columnBuilder.BuildColumns(grid);
		var secondCount = grid.Columns.Count;

		secondCount.Should().Be(firstCount);
	}
}
