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
public sealed class RecipeCoordinatorTests : IAsyncLifetime
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
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepAppended>()
			.Which.Index.Should().Be(0);
	}

	[AvaloniaFact]
	public void AppendStep_ReturnsSuggestedSelection_AsLastIndex()
	{
		_fixture.Session.Reset();

		var result = _fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void InsertStep_EmitsStepsInsertedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsInserted>()
			.Which.Should().BeEquivalentTo(new MutationSignal.StepsInserted(0, 1));
	}

	[AvaloniaFact]
	public void InsertStep_ReturnsSuggestedSelection_AsInsertedIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.InsertStep(0, RecipeTestDriver.WaitActionId);

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_EmitsStepRemovedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.RemoveStep(0);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepRemoved>()
			.Which.RemovedIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveStep_ReturnsNullSelection_WhenRecipeBecomesEmpty()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveStep(0);

		result.Value.Should().BeNull();
	}

	[AvaloniaFact]
	public void RemoveStep_ReturnsClampedSelection_WhenRemovingLast()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveStep(2);

		result.Value.Should().Be(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_EmitsStepsRemovedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepsRemoved>();
	}

	[AvaloniaFact]
	public void ChangeStepAction_EmitsStepActionChangedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.StepActionChanged>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void UpdateStepProperty_EmitsPropertyUpdatedSignal_WithCorrectStepIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.PropertyUpdated>()
			.Which.StepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void Undo_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.Undo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void Redo_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.Undo();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.Redo();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_EmitsRecipeReplacedSignal()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.NewRecipe();

		signals.Should().ContainSingle().Which.Should().BeOfType<MutationSignal.RecipeReplaced>();
	}

	[AvaloniaFact]
	public void NewRecipe_ClearsPriorNonStructuralEntries()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(9999);

		_fixture.Coordinator.NewRecipe();

		_fixture.MessagePanel.Entries.Should().NotContain(e => !e.IsStructural);
	}

	[AvaloniaFact]
	public void AppendStep_Failure_DoesNotEmitSignal()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);
		var signals = sink.Signals;

		_fixture.Coordinator.AppendStep(9999);

		signals.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_AddsErrorToMessagePanel()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(9999);

		_fixture.MessagePanel.ErrorCount.Should().BeGreaterThan(0);
	}

	[AvaloniaFact]
	public void IsDirty_True_AfterMutation()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanUndo_True_AfterMutation()
	{
		_fixture.Session.Reset();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.CanUndo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CanRedo_True_AfterUndoingMutation()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.Undo();

		_fixture.Coordinator.CanRedo.Should().BeTrue();
	}

	[AvaloniaFact]
	public void AppendStep_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();

		var result = _fixture.Coordinator.AppendStep(9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ChangeStepAction_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.ChangeStepAction(0, 9999);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void UpdateStepProperty_Failure_ReturnsFailed()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.UpdateStepProperty(0, "NonExistentColumn", "value");

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public void InsertSteps_ReturnsSuggestedSelection_AsStartIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.InsertSteps(0, new[] { _fixture.Coordinator.CurrentRecipe.Steps[0] });

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void ChangeStepAction_ReturnsSuggestedSelection_AsStepIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.ChangeStepAction(1, RecipeTestDriver.ForLoopActionId);

		result.Value.Should().Be(1);
	}

	[AvaloniaFact]
	public void RemoveSteps_ReturnsClampedSelection_AsMinIndex()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveSteps(new[] { 1, 2 });

		result.Value.Should().Be(0);
	}

	[AvaloniaFact]
	public void RemoveSteps_ReturnsNull_WhenRecipeBecomesEmpty()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var result = _fixture.Coordinator.RemoveSteps(new[] { 0, 1 });

		result.Value.Should().BeNull();
	}

	[AvaloniaFact]
	public void Attach_CalledTwice_Throws()
	{
		_fixture.Session.Reset();
		var sink = new RecordingRecipeSink();
		_fixture.Coordinator.Attach(sink);

		var act = () => _fixture.Coordinator.Attach(new RecordingRecipeSink());

		act.Should().Throw<InvalidOperationException>();
	}

	[AvaloniaFact]
	public async Task SaveRecipeAsync_Success_RaisesMutatedEvent()
	{
		_fixture.Session.Reset();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var mutatedCount = 0;
		_fixture.Coordinator.Mutated += () => mutatedCount++;
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"SemiStep.MutatedTest.{Guid.NewGuid():N}.csv");

		try
		{
			var result = await _fixture.Coordinator.SaveRecipeAsync(tempFilePath);

			result.IsSuccess.Should().BeTrue();
			mutatedCount.Should().BeGreaterThan(0,
				"SaveRecipeAsync must raise Mutated after MarkSaved so the window title and IsDirty refresh");
			_fixture.Coordinator.IsDirty.Should().BeFalse();
		}
		finally
		{
			if (File.Exists(tempFilePath))
			{
				File.Delete(tempFilePath);
			}
		}
	}
}
