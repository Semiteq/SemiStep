using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridViewModelTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeGridViewModel _grid = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_grid = new RecipeGridViewModel(_fixture.Coordinator, _fixture.RecipeMetadataRegistry, _fixture.MessagePanel);
		_grid.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Initialize_EmptyRecipe_HasZeroRows()
	{
		_fixture.Coordinator.NewRecipe();

		_grid.RecipeRows.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_AddsOneRow()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void AppendStep_RowHasCorrectActionId()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void AppendStep_RowStepNumberIsOne_ForFirstRow()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[AvaloniaFact]
	public void InsertStep_InsertsRowAtCorrectIndex()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.InsertStep(1, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[1].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void InsertStep_RenumbersSubsequentRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[1].StepNumber.Should().Be(2);
		_grid.RecipeRows[2].StepNumber.Should().Be(3);
	}

	[AvaloniaFact]
	public void RemoveStep_ReducesRowCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void RemoveStep_RenumbersRemainingRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveStep(0);

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
		_grid.RecipeRows[1].StepNumber.Should().Be(2);
	}

	[AvaloniaFact]
	public void RemoveSteps_RemovesMultipleRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 2 });

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_RenumbersRemainingRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[AvaloniaFact]
	public void ChangeStepAction_RebuildsRow_WithNewActionId()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void NewRecipe_ClearsAllRows()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.NewRecipe();

		_grid.RecipeRows.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void FullRebuild_RowCountMatchesRecipeStepCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();
		_fixture.Coordinator.Undo();

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void SelectedRowIndex_UpdatedAfterAppend()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.SelectedRowIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void CanDeleteStep_False_Initially()
	{
		_fixture.Coordinator.NewRecipe();

		_grid.CanDeleteStep.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanDeleteStep_True_WhenRowSelected()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.SelectedRowIndices = new[] { 0 };

		_grid.CanDeleteStep.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CollectSelectedSteps_ReturnsStepsInIndexOrder()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 2, 0 };

		var steps = _grid.CollectSelectedSteps();

		steps.Should().HaveCount(2);
		var recipe = _fixture.Coordinator.CurrentRecipe;
		steps[0].Should().Be(recipe.Steps[0]);
		steps[1].Should().Be(recipe.Steps[2]);
	}

	[AvaloniaFact]
	public void PropertyUpdated_UpdatesRowInPlace_WithoutChangingCount()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "15");

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[AvaloniaFact]
	public void StepStartTimes_RefreshedAfterMutation()
	{
		_fixture.Coordinator.NewRecipe();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].StepStartTime.Should().NotBeNull();
	}
}
