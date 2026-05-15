using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeMutationCoordinatorTests : IAsyncLifetime
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
	public void AppendStep_EmitsStepAppendedSignal()
	{
		_fixture.Workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepAppended>()
			.Which.Index.Should().Be(0);
	}

	[AvaloniaFact]
	public void AppendStep_SetsSuggestedSelection_ToLastIndex()
	{
		_fixture.Workspace.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var selection = _fixture.Coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(0);
	}

	[AvaloniaFact]
	public void InsertStep_EmitsStepsInsertedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsInserted>()
			.Which.Should().BeEquivalentTo(new MutationSignal.StepsInserted(0, 1));
	}

	[AvaloniaFact]
	public void InsertStep_SetsSuggestedSelection_ToInsertedIndex()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);
		var selection = _fixture.Coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_EmitsStepRemovedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.RemoveStep(0);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepRemoved>()
			.Which.RemovedIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_SuggestedSelection_IsNull_WhenRecipeBecomesEmpty()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();

		_fixture.Coordinator.RemoveStep(0);
		var selection = _fixture.Coordinator.ConsumeSuggestedSelection();

		selection.Should().BeNull();
	}

	[AvaloniaFact]
	public void RemoveStep_SuggestedSelection_ClampedToLastIndex_WhenRemovingLast()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();

		_fixture.Coordinator.RemoveStep(2);
		var selection = _fixture.Coordinator.ConsumeSuggestedSelection();

		selection.Should().Be(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_EmitsStepsRemovedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsRemoved>();
	}

	[AvaloniaFact]
	public void ChangeStepAction_EmitsStepActionChangedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepActionChanged>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void UpdateStepProperty_EmitsPropertyUpdatedSignal_WithCorrectStepIndex()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.PropertyUpdated>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void Undo_EmitsRecipeReplacedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.ConsumeSuggestedSelection();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.Undo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void Redo_EmitsRecipeReplacedSignal()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.Redo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_EmitsRecipeReplacedSignal()
	{
		_fixture.Workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.NewRecipe();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_ClearsPriorNonStructuralEntries()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(9999);

		_fixture.Coordinator.NewRecipe();

		_fixture.MessagePanel.Entries.Should().NotContain(e => !e.IsStructural);
	}

	[AvaloniaFact]
	public void ConsumeSuggestedSelection_ReturnsValueOnce_ThenNull()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var first = _fixture.Coordinator.ConsumeSuggestedSelection();
		var second = _fixture.Coordinator.ConsumeSuggestedSelection();

		first.Should().NotBeNull();
		second.Should().BeNull();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_DoesNotEmitSignal()
	{
		_fixture.Workspace.Reset();
		var signals = new List<MutationSignal>();
		using var sub = _fixture.Coordinator.StateChanged.Subscribe(signals.Add);

		_fixture.Coordinator.AppendStep(9999);

		signals.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_AddsErrorToMessagePanel()
	{
		_fixture.Workspace.Reset();

		_fixture.Coordinator.AppendStep(9999);

		_fixture.MessagePanel.ErrorCount.Should().BeGreaterThan(0);
	}

	[AvaloniaFact]
	public void IsDirty_True_AfterMutation()
	{
		_fixture.Workspace.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanUndo_True_AfterMutation()
	{
		_fixture.Workspace.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.CanUndo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanRedo_True_AfterUndoingMutation()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();

		_fixture.Coordinator.CanRedo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_ReturnsFailed()
	{
		_fixture.Workspace.Reset();

		var result = _fixture.Coordinator.AppendStep(9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ChangeStepAction_Failure_ReturnsFailed()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.ChangeStepAction(0, 9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void UpdateStepProperty_Failure_ReturnsFailed()
	{
		_fixture.Workspace.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.UpdateStepProperty(0, "NonExistentColumn", "value");

		result.IsFailed.Should().BeTrue();
	}
}
