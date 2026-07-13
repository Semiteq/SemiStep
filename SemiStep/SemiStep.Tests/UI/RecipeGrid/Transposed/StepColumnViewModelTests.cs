using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class StepColumnViewModelTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void BuildFromRegistry_MatchesRegistryColumnOrderAndMetadata()
	{
		var descriptors = ParameterDescriptor.BuildFromRegistry(_fixture.RecipeMetadataRegistry);
		var columns = _fixture.RecipeMetadataRegistry.GetAllColumns();

		descriptors.Should().HaveCount(columns.Count);
		for (var i = 0; i < columns.Count; i++)
		{
			descriptors[i].ParameterKey.Should().Be(columns[i].Key);
			descriptors[i].ParameterDisplayName.Should().Be(columns[i].UiName);
			descriptors[i].ColumnType.Should().Be(columns[i].ColumnType);
			descriptors[i].IsReadOnlyParameter.Should().Be(columns[i].ReadOnly);
		}
	}

	[AvaloniaFact]
	public void BuildFromRegistry_KeepsConfiguredColumnSequence()
	{
		var descriptors = ParameterDescriptor.BuildFromRegistry(_fixture.RecipeMetadataRegistry);

		descriptors.Select(descriptor => descriptor.ParameterKey).Should().ContainInOrder(
			ColumnTypes.Action,
			RecipeTestDriver.StepDurationColumn,
			RecipeTestDriver.TaskColumn,
			RecipeTestDriver.TargetColumn,
			RecipeTestDriver.CommentColumn);
	}

	[AvaloniaFact]
	public void Constructor_BuildsCellsInDescriptorOrder()
	{
		var column = CreateColumn(out var descriptors);

		column.Cells.Should().HaveCount(descriptors.Count);
		column.Cells.Select(cell => cell.Descriptor).Should().Equal(descriptors);
	}

	[AvaloniaFact]
	public void Constructor_ComputesInapplicableColumnsLikeCanonical()
	{
		var column = CreateColumn(out _);
		var step = _fixture.Coordinator.CurrentRecipe.Steps[0];
		var action = _fixture.RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var expected = RecipeRowViewModel.BuildInapplicableColumns(
			action,
			step,
			_fixture.RecipeMetadataRegistry);

		column.Row.InapplicableColumns.Should().BeEquivalentTo(expected);
	}

	[AvaloniaFact]
	public void Dispose_CascadesToWrappedRowViewModel()
	{
		var column = CreateColumn(out _);
		var propertyWrites = new List<(string Key, string? Value)>();
		column.Row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));

		column.Dispose();
		column.Row.SetPropertyValue(RecipeTestDriver.CommentColumn, "after dispose");

		propertyWrites.Should().BeEmpty();
	}

	private StepColumnViewModel CreateColumn(out IReadOnlyList<ParameterDescriptor> descriptors)
	{
		_fixture.SeedRecipe(1);
		var step = _fixture.Coordinator.CurrentRecipe.Steps[0];
		var action = _fixture.RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		descriptors = ParameterDescriptor.BuildFromRegistry(_fixture.RecipeMetadataRegistry);

		return new StepColumnViewModel(
			1,
			step,
			action,
			_fixture.RecipeMetadataRegistry,
			descriptors,
			(row, descriptor) => new TestParameterCellViewModel(row, descriptor));
	}

	private sealed class TestParameterCellViewModel(
		RecipeRowViewModel recipeRowViewModel,
		ParameterDescriptor parameterDescriptor)
		: ParameterCellViewModel(recipeRowViewModel, parameterDescriptor);
}
