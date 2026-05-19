using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

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
	public void EditingTemplate_CommentColumn_SetsTextBoxMaxLengthFromPropertyDefinition()
	{
		var commentColumn = _fixture.RecipeMetadataRegistry.GetColumn("comment").Value;
		var propertyDefinition = _fixture.RecipeMetadataRegistry.GetProperty(commentColumn.PropertyTypeId).Value;
		propertyDefinition.MaxLength.Should().NotBeNull(
			"the test configuration must declare MaxLength on the string property for this assertion to be meaningful");

		var template = TextCellFactory.CreateEditingTemplate(commentColumn.Key, propertyDefinition.MaxLength);
		var row = CreateRow();

		var built = template.Build(row);
		built.Should().NotBeNull();
		var textBox = built as TextBox;
		textBox.Should().NotBeNull("CellEditingTemplate must materialize a TextBox for string columns");

		textBox!.MaxLength.Should().Be(propertyDefinition.MaxLength!.Value);
	}

	[AvaloniaFact]
	public void EditingTemplate_ColumnWithoutMaxLength_LeavesTextBoxMaxLengthAtDefault()
	{
		var taskColumn = _fixture.RecipeMetadataRegistry.GetColumn("task").Value;
		var propertyDefinition = _fixture.RecipeMetadataRegistry.GetProperty(taskColumn.PropertyTypeId).Value;
		propertyDefinition.MaxLength.Should().BeNull(
			"the test configuration must declare no MaxLength on this property for this assertion to be meaningful");

		var template = TextCellFactory.CreateEditingTemplate(taskColumn.Key, propertyDefinition.MaxLength);
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
