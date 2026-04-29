using Avalonia.Threading;

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

using Xunit;

namespace Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeMutationCoordinatorTests : IAsyncLifetime
{
	private RecipeWorkspace _workspace = null!;
	private RecipeEditor _editor = null!;
	private PlcLifecycleManager _plc = null!;
	private MessagePanelViewModel _messagePanel = null!;
	private RecipeMutationCoordinator _coordinator = null!;

	public async Task InitializeAsync()
	{
		var (services, workspace, editor, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		_workspace = workspace;
		_editor = editor;
		_plc = plc;
		var configRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		_messagePanel = new MessagePanelViewModel();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var queryService = new RecipeQueryService(_workspace, _plc, clipboardSerializer, importedRecipeValidator, configRegistry);
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
			_messagePanel);
		_coordinator.Initialize();
	}

	public Task DisposeAsync()
	{
		_coordinator.Dispose();
		_messagePanel.Dispose();
		return Task.CompletedTask;
	}

	[Fact]
	public void AppendStep_EmitsStepAppendedSignal()
	{
		_workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepAppended>()
			.Which.Index.Should().Be(0);
	}

	[Fact]
	public void AppendStep_SetsSuggestedSelection_ToLastIndex()
	{
		_workspace.Reset();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var selection = _coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(0);
	}

	[Fact]
	public void InsertStep_EmitsStepsInsertedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsInserted>()
			.Which.Should().BeEquivalentTo(new MutationSignal.StepsInserted(0, 1));
	}

	[Fact]
	public void InsertStep_SetsSuggestedSelection_ToInsertedIndex()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();

		_coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		var selection = _coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(0);
	}

	[Fact]
	public void RemoveStep_EmitsStepRemovedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.RemoveStep(0);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepRemoved>()
			.Which.RemovedIndex.Should().Be(0);
	}

	[Fact]
	public void RemoveStep_SuggestedSelection_IsNull_WhenRecipeBecomesEmpty()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();

		_coordinator.RemoveStep(0);
		var selection = _coordinator.ConsumeSuggestedSelection();

		selection.Should().BeNull();
	}

	[Fact]
	public void RemoveStep_SuggestedSelection_ClampedToLastIndex_WhenRemovingLast()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();

		_coordinator.RemoveStep(2);
		var selection = _coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(1);
	}

	[Fact]
	public void RemoveSteps_EmitsStepsRemovedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.RemoveSteps(new[] { 0, 1 });

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsRemoved>();
	}

	[Fact]
	public void ChangeStepAction_EmitsStepActionChangedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepActionChanged>()
			.Which.StepIndex.Should().Be(0);
	}

	[Fact]
	public void UpdateStepProperty_EmitsPropertyUpdatedSignal_WithCorrectStepIndex()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.PropertyUpdated>()
			.Which.StepIndex.Should().Be(0);
	}

	[Fact]
	public void Undo_EmitsRecipeReplacedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.Undo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[Fact]
	public void Redo_EmitsRecipeReplacedSignal()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_coordinator.Undo();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.Redo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[Fact]
	public void NewRecipe_EmitsRecipeReplacedSignal()
	{
		_workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.NewRecipe();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[Fact]
	public void NewRecipe_ClearsPriorNonStructuralEntries()
	{
		_workspace.Reset();
		_coordinator.AppendStep(9999);
		Dispatcher.UIThread.RunJobs(null);

		_coordinator.NewRecipe();
		Dispatcher.UIThread.RunJobs(null);

		_messagePanel.Entries.Should().NotContain(e => !e.IsStructural);
	}

	[Fact]
	public void ConsumeSuggestedSelection_ReturnsValueOnce_ThenNull()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var first = _coordinator.ConsumeSuggestedSelection();
		var second = _coordinator.ConsumeSuggestedSelection();

		first.Should().NotBeNull();
		second.Should().BeNull();
	}

	[Fact]
	public void AppendStep_Failure_DoesNotEmitSignal()
	{
		_workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _coordinator.StateChanged.Subscribe(signals.Add);

		_coordinator.AppendStep(9999);

		signals.Should().BeEmpty();
	}

	[Fact]
	public void AppendStep_Failure_AddsErrorToMessagePanel()
	{
		_workspace.Reset();

		_coordinator.AppendStep(9999);
		Dispatcher.UIThread.RunJobs(null);

		_messagePanel.ErrorCount.Should().BeGreaterThan(0);
	}

	[Fact]
	public void IsDirty_True_AfterMutation()
	{
		_workspace.Reset();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.IsDirty.Should().BeTrue();
	}

	[Fact]
	public void CanUndo_True_AfterMutation()
	{
		_workspace.Reset();

		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.CanUndo.Should().BeTrue();
	}

	[Fact]
	public void CanRedo_True_AfterUndoingMutation()
	{
		_workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_coordinator.Undo();

		_coordinator.CanRedo.Should().BeTrue();
	}

	[Fact]
	public void AppendStep_Failure_ReturnsFailed()
	{
		_ = _workspace.Reset();

		var result = _coordinator.AppendStep(9999);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ChangeStepAction_Failure_ReturnsFailed()
	{
		_ = _workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _coordinator.ChangeStepAction(0, 9999);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void UpdateStepProperty_Failure_ReturnsFailed()
	{
		_ = _workspace.Reset();
		_coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _coordinator.UpdateStepProperty(0, "NonExistentColumn", "value");

		result.IsFailed.Should().BeTrue();
	}

}
