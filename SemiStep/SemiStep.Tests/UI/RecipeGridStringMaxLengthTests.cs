using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class RecipeGridStringMaxLengthTests : IAsyncLifetime
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
	public void ColumnBuilder_CommentColumn_EditingTemplateMaterializesTextBoxWithMaxLengthFromProperty()
	{
		var commentColumn = _fixture.RecipeMetadataRegistry.GetColumn("comment").Value;
		var propertyDefinition = _fixture.RecipeMetadataRegistry.GetProperty(commentColumn.PropertyTypeId).Value;
		propertyDefinition.MaxLength.Should().NotBeNull(
			"the test configuration must declare MaxLength on the string property for this assertion to be meaningful");

		var columnBuilder = new ColumnBuilder(GridStyleOptions.Default, _fixture.RecipeMetadataRegistry);
		var grid = new DataGrid();
		columnBuilder.BuildColumns(grid);

		var templateColumn = grid.Columns
			.OfType<DataGridTemplateColumn>()
			.FirstOrDefault(column => string.Equals(column.Tag as string, "comment", StringComparison.Ordinal));
		templateColumn.Should().NotBeNull("ColumnBuilder must produce a template column for the comment field");
		templateColumn!.CellEditingTemplate.Should().NotBeNull(
			"editable string columns must wire a CellEditingTemplate so MaxLength can be applied at edit time");

		var row = CreateRow();
		var built = templateColumn.CellEditingTemplate!.Build(row);
		built.Should().NotBeNull();
		var textBox = built as TextBox;
		textBox.Should().NotBeNull("CellEditingTemplate must materialize a TextBox for string columns");

		textBox!.MaxLength.Should().Be(propertyDefinition.MaxLength!.Value,
			"the ColumnBuilder path must propagate the property MaxLength SoT to the TextBox");
	}

	[AvaloniaFact]
	public void EditingTemplate_NullMaxLength_LeavesTextBoxMaxLengthAtDefault()
	{
		// Direct factory contract: when no MaxLength is provided, TextBox.MaxLength must remain
		// at the framework default (0 = unlimited). The integration test above covers the
		// ColumnBuilder end-to-end path for the live string column; this complements it by pinning
		// the null-branch contract that no live column currently exercises.
		var template = TextCellFactory.CreateEditingTemplate("any_key", maxLength: null);
		var row = CreateRow();

		var built = template.Build(row);
		built.Should().NotBeNull();
		var textBox = built as TextBox;
		textBox.Should().NotBeNull();

		textBox!.MaxLength.Should().Be(0);
	}

	private RecipeRowViewModel CreateRow()
	{
		var firstAction = _fixture.RecipeMetadataRegistry.GetAllActions().First();
		var step = new Step(firstAction.Id, ImmutableDictionary<PropertyId, PropertyValue>.Empty);
		return new RecipeRowViewModel(
			1,
			step,
			firstAction,
			_fixture.RecipeMetadataRegistry,
			new HashSet<string>(StringComparer.OrdinalIgnoreCase));
	}
}
