using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;

using Tests.Core.Helpers;
using Tests.Helpers;

using UI.Coordinator;
using UI.MessageService;
using UI.RecipeGrid;

using Xunit;

namespace Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridViewModelTests : IAsyncLifetime
{
	private RecipeWorkspace _workspace = null!;
	private RecipeEditor _editor = null!;
	private PlcLifecycleManager _plc = null!;
	private MessagePanelViewModel _panel = null!;
	private RecipeMutationCoordinator _coordinator = null!;
	private RecipeGridViewModel _grid = null!;
	private ConfigRegistry _configRegistry = null!;

	public async Task InitializeAsync()
	{
		var (services, workspace, editor, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		_workspace = workspace;
		_editor = editor;
		_plc = plc;
		_configRegistry = services.GetRequiredService<ConfigRegistry>();
		_panel = new MessagePanelViewModel();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var queryService = new RecipeQueryService(_workspace, _plc, clipboardSerializer, importedRecipeValidator, _configRegistry);
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		_coordinator = new RecipeMutationCoordinator(
			_workspace,
			_editor,
			_plc,
			csvService,
			importedRecipeValidator,
			appConfiguration,
			queryService,
			_panel);
		_coordinator.Initialize();
		_grid = new RecipeGridViewModel(_coordinator, _configRegistry, _panel);
		_grid.Initialize();
	}

	public Task DisposeAsync()
	{
		_grid.Dispose();
		_coordinator.Dispose();
		_panel.Dispose();
		return Task.CompletedTask;
	}

	[Fact]
	public void Initialize_EmptyRecipe_HasZeroRows()
	{
		_coordinator.NewRecipe();

		_grid.RecipeRows.Should().BeEmpty();
	}

	[Fact]
	public void AppendStep_AddsOneRow()
	{
		_coordinator.NewRecipe();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[Fact]
	public void AppendStep_RowHasCorrectActionId()
	{
		_coordinator.NewRecipe();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[Fact]
	public void AppendStep_RowStepNumberIsOne_ForFirstRow()
	{
		_coordinator.NewRecipe();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[Fact]
	public void InsertStep_InsertsRowAtCorrectIndex()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.InsertStep(1, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[1].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[Fact]
	public void InsertStep_RenumbersSubsequentRows()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.InsertStep(0, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[1].StepNumber.Should().Be(2);
		_grid.RecipeRows[2].StepNumber.Should().Be(3);
	}

	[Fact]
	public void RemoveStep_ReducesRowCount()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.RemoveStep(0);

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[Fact]
	public void RemoveStep_RenumbersRemainingRows()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.RemoveStep(0);

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
		_grid.RecipeRows[1].StepNumber.Should().Be(2);
	}

	[Fact]
	public void RemoveSteps_RemovesMultipleRows()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.RemoveSteps(new[] { 0, 2 });

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[Fact]
	public void RemoveSteps_RenumbersRemainingRows()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.RemoveSteps(new[] { 0, 1 });

		_grid.RecipeRows[0].StepNumber.Should().Be(1);
	}

	[Fact]
	public void ChangeStepAction_RebuildsRow_WithNewActionId()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_grid.RecipeRows[0].ActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[Fact]
	public void NewRecipe_ClearsAllRows()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.NewRecipe();

		_grid.RecipeRows.Should().BeEmpty();
	}

	[Fact]
	public void FullRebuild_RowCountMatchesRecipeStepCount()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.Undo();
		_coordinator.Undo();

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[Fact]
	public void SelectedRowIndex_UpdatedAfterAppend()
	{
		_coordinator.NewRecipe();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.SelectedRowIndex.Should().Be(0);
	}

	[Fact]
	public void CanDeleteStep_False_Initially()
	{
		_coordinator.NewRecipe();

		_grid.CanDeleteStep.Should().BeFalse();
	}

	[Fact]
	public void CanDeleteStep_True_WhenRowSelected()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.SelectedRowIndices = new[] { 0 };

		_grid.CanDeleteStep.Should().BeTrue();
	}

	[Fact]
	public void CollectSelectedSteps_ReturnsStepsInIndexOrder()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_grid.SelectedRowIndices = new[] { 2, 0 };

		var steps = _grid.CollectSelectedSteps();

		steps.Should().HaveCount(2);
		var recipe = _coordinator.CurrentRecipe;
		steps[0].Should().Be(recipe.Steps[0]);
		steps[1].Should().Be(recipe.Steps[2]);
	}

	[Fact]
	public void PropertyUpdated_UpdatesRowInPlace_WithoutChangingCount()
	{
		_coordinator.NewRecipe();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "15");

		_grid.RecipeRows.Should().HaveCount(1);
	}

	[Fact]
	public void StepStartTimes_RefreshedAfterMutation()
	{
		_coordinator.NewRecipe();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_grid.RecipeRows[0].StepStartTime.Should().NotBeNull();
	}
}
