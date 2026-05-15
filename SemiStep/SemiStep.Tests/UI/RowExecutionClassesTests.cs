using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class RowExecutionClassesTests : IAsyncLifetime
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
	public void Apply_DefaultRow_AddsNeitherClass()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();

		RowExecutionClasses.Apply(dataGridRow, row);

		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.CurrentStepClass);
		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.PastStepClass);
	}

	[AvaloniaFact]
	public void Apply_CurrentStep_AddsCurrentStepClass()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();
		row.IsCurrentStep = true;

		RowExecutionClasses.Apply(dataGridRow, row);

		dataGridRow.Classes.Should().Contain(RowExecutionClasses.CurrentStepClass);
		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.PastStepClass);
	}

	[AvaloniaFact]
	public void Apply_PastStep_AddsPastStepClass()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();
		row.IsPastStep = true;

		RowExecutionClasses.Apply(dataGridRow, row);

		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.CurrentStepClass);
		dataGridRow.Classes.Should().Contain(RowExecutionClasses.PastStepClass);
	}

	[AvaloniaFact]
	public void Apply_TransitionFromCurrentToPast_TogglesClasses()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();
		row.IsCurrentStep = true;
		RowExecutionClasses.Apply(dataGridRow, row);

		row.IsCurrentStep = false;
		row.IsPastStep = true;
		RowExecutionClasses.Apply(dataGridRow, row);

		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.CurrentStepClass);
		dataGridRow.Classes.Should().Contain(RowExecutionClasses.PastStepClass);
	}

	[AvaloniaFact]
	public void Apply_RepeatedInvocation_DoesNotDuplicateClass()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();
		row.IsCurrentStep = true;

		RowExecutionClasses.Apply(dataGridRow, row);
		RowExecutionClasses.Apply(dataGridRow, row);

		dataGridRow.Classes.Count(c => c == RowExecutionClasses.CurrentStepClass).Should().Be(1);
	}

	[AvaloniaFact]
	public void Clear_RemovesBothClasses()
	{
		var dataGridRow = new DataGridRow();
		var row = CreateRow();
		row.IsCurrentStep = true;
		row.IsPastStep = true;
		RowExecutionClasses.Apply(dataGridRow, row);

		RowExecutionClasses.Clear(dataGridRow);

		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.CurrentStepClass);
		dataGridRow.Classes.Should().NotContain(RowExecutionClasses.PastStepClass);
	}

	private RecipeRowViewModel CreateRow()
	{
		var action = _fixture.RecipeMetadataRegistry.GetAction(RecipeTestDriver.WaitActionId).Value;
		var step = new Step(RecipeTestDriver.WaitActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty);
		var inapplicableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return new RecipeRowViewModel(1, step, action, _fixture.RecipeMetadataRegistry, inapplicableColumns);
	}
}
