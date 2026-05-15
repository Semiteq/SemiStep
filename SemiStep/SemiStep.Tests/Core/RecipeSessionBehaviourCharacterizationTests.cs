using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core;

/// <summary>
/// Characterization tests pinning the same observable contract as
/// <see cref="RecipeBehaviourCharacterizationTests"/> but exercised through the
/// merged <see cref="RecipeSession"/> API. Divergence between the two suites is a
/// behaviour bug: the merge must be invisible to callers. Once the legacy classes
/// are deleted in Task 11 the older suite is removed and this one remains.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Characterization")]
public sealed class RecipeSessionBehaviourCharacterizationTests
{
	private const int WaitActionId = RecipeTestDriver.WaitActionId;
	private const int ForLoopActionId = RecipeTestDriver.ForLoopActionId;
	private const int PauseActionId = RecipeTestDriver.PauseActionId;
	private const int EndForLoopActionId = RecipeTestDriver.EndForLoopActionId;
	private const int UnknownActionId = 9999;
	private const string DurationColumn = RecipeTestDriver.StepDurationColumn;
	private const string CommentColumn = RecipeTestDriver.CommentColumn;
	private const string TaskColumn = RecipeTestDriver.TaskColumn;

	#region Session.Apply

	[Fact]
	public async Task Apply_ValidRecipe_UpdatesCurrent_AndKeepsDirtyTrue()
	{
		var harness = await BuildHarnessAsync();

		harness.Session.MarkSaved();
		harness.Session.IsDirty.Should().BeFalse("MarkSaved was just called");

		var newRecipe = harness.Session.Current.AppendStep(BuildWaitStep(harness, 10f));

		var result = harness.Session.Apply(newRecipe);

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(1);
		harness.Session.IsDirty.Should().BeTrue("any successful Apply flips the session dirty");
	}

	[Fact]
	public async Task Apply_InvalidRecipe_ReturnsFailureAndDoesNotMutateState()
	{
		var harness = await BuildHarnessAsync();

		AppendWait(harness, 1f);
		harness.Session.AppendStep(ForLoopActionId).IsSuccess.Should().BeTrue();
		harness.Session.AppendStep(ForLoopActionId).IsSuccess.Should().BeTrue();
		harness.Session.AppendStep(ForLoopActionId).IsSuccess.Should().BeTrue();
		harness.Session.AppendStep(ForLoopActionId).IsSuccess.Should().BeTrue();
		AppendWait(harness, 1f);
		var stepCountBefore = harness.Session.Current.StepCount;

		var failingResult = harness.Session.AppendStep(EndForLoopActionId);

		failingResult.IsFailed.Should().BeTrue("closing a 4th nested loop exceeds the max nesting depth");
		harness.Session.Current.StepCount.Should().Be(
			stepCountBefore,
			"a rejected Apply must not commit the new recipe to state");
	}

	[Fact]
	public async Task Apply_PushesPriorRecipeOntoUndoStack()
	{
		var harness = await BuildHarnessAsync();

		harness.Session.CanUndo.Should().BeFalse();

		var newRecipe = harness.Session.Current.AppendStep(BuildWaitStep(harness, 10f));
		harness.Session.Apply(newRecipe).IsSuccess.Should().BeTrue();

		harness.Session.CanUndo.Should().BeTrue("Apply pushes the previous recipe so it can be undone");
	}

	#endregion

	#region Session.Reset

	[Fact]
	public async Task Reset_ClearsRecipeAndHistory()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		AppendWait(harness, 10f);

		harness.Session.CanUndo.Should().BeTrue();

		harness.Session.Reset();

		harness.Session.CanUndo.Should().BeFalse("Reset clears the undo history");
		harness.Session.CanRedo.Should().BeFalse("Reset clears the redo history");
		harness.Session.Current.StepCount.Should().Be(0);
	}

	[Fact]
	public async Task Reset_LeavesSessionDirty_BecauseEmptyAnalysisFlipsTheFlag()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.MarkSaved();
		harness.Session.IsDirty.Should().BeFalse();

		harness.Session.Reset();

		harness.Session.IsDirty.Should().BeTrue(
			"the current Reset implementation runs analyzer.Analyze(Empty) which flips IsDirty back on via the snapshot update");
	}

	#endregion

	#region Session.MarkSaved

	[Fact]
	public async Task MarkSaved_ClearsDirtyButPreservesHistory()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 10f);
		AppendWait(harness, 20f);

		harness.Session.IsDirty.Should().BeTrue();
		harness.Session.CanUndo.Should().BeTrue();

		harness.Session.MarkSaved();

		harness.Session.IsDirty.Should().BeFalse("MarkSaved is the persistence-confirmation signal");
		harness.Session.CanUndo.Should().BeTrue("MarkSaved must not erase the undo stack");
		harness.Session.Current.StepCount.Should().Be(2, "MarkSaved is a flag flip; the recipe stays put");
	}

	#endregion

	#region Session.AppendStep

	[Fact]
	public async Task AppendStep_ValidAction_Succeeds_AndAppendsAtEnd()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.AppendStep(WaitActionId);

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(2);
		harness.Session.Current.Steps[^1].ActionKey.Should().Be(WaitActionId);
	}

	[Fact]
	public async Task AppendStep_UnknownActionId_FailsAndDoesNotMutate()
	{
		var harness = await BuildHarnessAsync();

		var stepCountBefore = harness.Session.Current.StepCount;
		var result = harness.Session.AppendStep(UnknownActionId);

		result.IsFailed.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(stepCountBefore);
	}

	[Fact]
	public async Task AppendStep_ReturnsSuggestedSelectionAtNewLastIndex()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.AppendStep(PauseActionId);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().Be(1, "AppendStep reports the index of the newly appended step");
	}

	#endregion

	#region Session.InsertStep / RemoveStep / RemoveSteps

	[Fact]
	public async Task InsertStep_OutOfRangeIndex_Fails()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.InsertStep(5, WaitActionId);

		result.IsFailed.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(1);
	}

	[Fact]
	public async Task InsertStep_PreservesOrderingOfExistingSteps()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		harness.Session.InsertStep(1, ForLoopActionId).IsSuccess.Should().BeTrue();

		var steps = harness.Session.Current.Steps;
		steps.Should().HaveCount(3);
		steps[0].ActionKey.Should().Be(WaitActionId);
		steps[1].ActionKey.Should().Be(ForLoopActionId);
		steps[2].ActionKey.Should().Be(PauseActionId);
	}

	[Fact]
	public async Task InsertStep_ReturnsSuggestedSelectionAtInsertIndex()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		var result = harness.Session.InsertStep(1, ForLoopActionId);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().Be(1);
	}

	[Fact]
	public async Task RemoveStep_PreservesOrderingOfRemainingSteps()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();
		AppendWait(harness, 15f);

		harness.Session.RemoveStep(1).IsSuccess.Should().BeTrue();

		var steps = harness.Session.Current.Steps;
		steps.Should().HaveCount(2);
		steps[0].ActionKey.Should().Be(WaitActionId);
		steps[1].ActionKey.Should().Be(WaitActionId);
	}

	[Fact]
	public async Task RemoveStep_SuggestsClampedIndex_WhenLastRowRemoved()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		var result = harness.Session.RemoveStep(1);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().Be(0, "removing the last row clamps suggestion to StepCount-1");
	}

	[Fact]
	public async Task RemoveStep_SuggestsNull_WhenRecipeBecomesEmpty()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.RemoveStep(0);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().BeNull("emptying the recipe leaves no selectable index");
	}

	[Fact]
	public async Task RemoveSteps_NonContiguous_RemovesIntendedIndicesOnly()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();
		AppendWait(harness, 15f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		harness.Session.RemoveSteps([0, 2]).IsSuccess.Should().BeTrue();

		var steps = harness.Session.Current.Steps;
		steps.Should().HaveCount(2);
		steps[0].ActionKey.Should().Be(PauseActionId);
		steps[1].ActionKey.Should().Be(PauseActionId);
	}

	[Fact]
	public async Task RemoveSteps_OutOfRangeIndex_Fails_AndKeepsRecipeIntact()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		var result = harness.Session.RemoveSteps([0, 999]);

		result.IsFailed.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(2);
	}

	#endregion

	#region Session.InsertSteps

	[Fact]
	public async Task InsertSteps_AtEnd_AppendsInOrder()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var stepsToInsert = new List<Step>
		{
			BuildWaitStep(harness, 11f),
			BuildPauseStep(harness)
		};

		harness.Session.InsertSteps(1, stepsToInsert).IsSuccess.Should().BeTrue();

		var steps = harness.Session.Current.Steps;
		steps.Should().HaveCount(3);
		steps[1].ActionKey.Should().Be(WaitActionId);
		steps[2].ActionKey.Should().Be(PauseActionId);
	}

	[Fact]
	public async Task InsertSteps_OutOfRangeStartIndex_Fails()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var stepsToInsert = new List<Step> { BuildPauseStep(harness) };

		var result = harness.Session.InsertSteps(99, stepsToInsert);

		result.IsFailed.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(1);
	}

	[Fact]
	public async Task InsertSteps_ReturnsSuggestedSelectionAtStartIndex()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var stepsToInsert = new List<Step>
		{
			BuildPauseStep(harness),
			BuildWaitStep(harness, 7f)
		};

		var result = harness.Session.InsertSteps(0, stepsToInsert);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().Be(0);
	}

	#endregion

	#region Session.ChangeStepAction

	[Fact]
	public async Task ChangeStepAction_ReplacesActionAndResetsToDefaults()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 10f);

		harness.Session.ChangeStepAction(0, ForLoopActionId).IsSuccess.Should().BeTrue();

		var step = harness.Session.Current.Steps[0];
		step.ActionKey.Should().Be(ForLoopActionId);
		step.Properties.ContainsKey(new PropertyId(DurationColumn))
			.Should().BeFalse("the For action does not define a step_duration column");
	}

	[Fact]
	public async Task ChangeStepAction_OutOfRangeIndex_Fails()
	{
		var harness = await BuildHarnessAsync();

		var result = harness.Session.ChangeStepAction(0, WaitActionId);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task ChangeStepAction_SameActionId_RebuildsStepToDefaults()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 10f);
		harness.Session.UpdateStepProperty(0, DurationColumn, "42").IsSuccess.Should().BeTrue();

		var durationBefore = harness.Session.Current.Steps[0]
			.Properties[new PropertyId(DurationColumn)]
			.AsFloat();
		durationBefore.Should().Be(42f);

		harness.Session.ChangeStepAction(0, WaitActionId).IsSuccess.Should().BeTrue();

		var durationAfter = harness.Session.Current.Steps[0]
			.Properties[new PropertyId(DurationColumn)]
			.AsFloat();
		durationAfter.Should().Be(10f, "the merged implementation preserves the legacy behaviour: rebuild from defaults even when the action id is unchanged");
	}

	[Fact]
	public async Task ChangeStepAction_ReturnsSuggestedSelectionAtTargetIndex()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 10f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		var result = harness.Session.ChangeStepAction(0, PauseActionId);

		result.IsSuccess.Should().BeTrue();
		result.Value.SuggestedSelectionIndex.Should().Be(0);
	}

	#endregion

	#region Session.UpdateStepProperty

	[Fact]
	public async Task UpdateStepProperty_ValidValue_UpdatesProperty()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.UpdateStepProperty(
			0,
			CommentColumn,
			"hello");

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.Steps[0]
			.Properties[new PropertyId(CommentColumn)]
			.AsString()
			.Should().Be("hello");
	}

	[Fact]
	public async Task UpdateStepProperty_UnknownColumn_Fails()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.UpdateStepProperty(0, "no_such_column", "1");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task UpdateStepProperty_NonParsableValue_Fails()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var result = harness.Session.UpdateStepProperty(0, DurationColumn, "not-a-number");

		result.IsFailed.Should().BeTrue();
	}

	#endregion

	#region History (undo / redo)

	[Fact]
	public async Task Undo_RestoresPreviousRecipe()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		harness.Session.Current.StepCount.Should().Be(2);

		harness.Session.Undo().IsSuccess.Should().BeTrue();

		harness.Session.Current.StepCount.Should().Be(1);
		harness.Session.Current.Steps[0].ActionKey.Should().Be(WaitActionId);
	}

	[Fact]
	public async Task Undo_WithEmptyUndoStack_Fails()
	{
		var harness = await BuildHarnessAsync();

		var result = harness.Session.Undo();

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task Redo_RestoresState_AfterUndo()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		harness.Session.Undo().IsSuccess.Should().BeTrue();
		harness.Session.CanRedo.Should().BeTrue();

		harness.Session.Redo().IsSuccess.Should().BeTrue();

		harness.Session.Current.StepCount.Should().Be(2);
		harness.Session.Current.Steps[1].ActionKey.Should().Be(PauseActionId);
	}

	[Fact]
	public async Task Redo_WithEmptyRedoStack_Fails()
	{
		var harness = await BuildHarnessAsync();

		var result = harness.Session.Redo();

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task NewMutationAfterUndo_ClearsRedoStack()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		harness.Session.AppendStep(PauseActionId).IsSuccess.Should().BeTrue();

		harness.Session.Undo().IsSuccess.Should().BeTrue();
		harness.Session.CanRedo.Should().BeTrue();

		harness.Session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();

		harness.Session.CanRedo.Should().BeFalse("any new mutation discards the redo stack");
	}

	#endregion

	#region LoadAsCurrent / LoadAsCurrentValidated

	[Fact]
	public async Task LoadAsCurrent_ReplacesRecipeAndClearsUndoRedo()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		AppendWait(harness, 10f);

		harness.Session.Undo();
		harness.Session.CanRedo.Should().BeTrue();
		harness.Session.CanUndo.Should().BeTrue();

		var loaded = Recipe.Empty.AppendStep(BuildPauseStep(harness));

		harness.Session.LoadAsCurrent(loaded).IsSuccess.Should().BeTrue();

		harness.Session.Current.StepCount.Should().Be(1);
		harness.Session.Current.Steps[0].ActionKey.Should().Be(PauseActionId);
		harness.Session.CanUndo.Should().BeFalse("LoadAsCurrent represents a fresh editing session");
		harness.Session.CanRedo.Should().BeFalse("LoadAsCurrent must wipe the redo stack");
	}

	[Fact]
	public async Task LoadAsCurrentValidated_RejectsInvalidRecipe_AndLeavesStateUnchanged()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var bogusRecipe = Recipe.Empty.AppendStep(new Step(
			UnknownActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty));

		var validator = harness.Services.GetRequiredService<ImportedRecipeValidator>();

		var result = harness.Session.LoadAsCurrentValidated(bogusRecipe, validator);

		result.IsFailed.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(1, "validation failure must not replace the live recipe");
		harness.Session.Current.Steps[0].ActionKey.Should().Be(WaitActionId);
	}

	[Fact]
	public async Task LoadAsCurrentValidated_AcceptsValidRecipe_AndReplacesState()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);

		var validRecipe = Recipe.Empty
			.AppendStep(BuildPauseStep(harness))
			.AppendStep(BuildWaitStep(harness, 7f));

		var validator = harness.Services.GetRequiredService<ImportedRecipeValidator>();

		var result = harness.Session.LoadAsCurrentValidated(validRecipe, validator);

		result.IsSuccess.Should().BeTrue();
		harness.Session.Current.StepCount.Should().Be(2);
		harness.Session.CanUndo.Should().BeFalse("a validated load resets history");
	}

	#endregion

	#region Snapshot / Query surface

	[Fact]
	public async Task Snapshot_AfterMutation_ReflectsCurrentRecipe()
	{
		var harness = await BuildHarnessAsync();
		AppendWait(harness, 5f);
		AppendWait(harness, 10f);

		var snapshot = harness.Session.Snapshot;

		snapshot.IsSuccess.Should().BeTrue();
		snapshot.Value.Recipe.StepCount.Should().Be(2);
		snapshot.Value.TotalDuration.Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public async Task CanUndoCanRedo_ReflectHistoryState()
	{
		var harness = await BuildHarnessAsync();

		harness.Session.CanUndo.Should().BeFalse();
		harness.Session.CanRedo.Should().BeFalse();

		harness.Session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();

		harness.Session.CanUndo.Should().BeTrue();
		harness.Session.CanRedo.Should().BeFalse();

		harness.Session.Undo().IsSuccess.Should().BeTrue();

		harness.Session.CanUndo.Should().BeFalse();
		harness.Session.CanRedo.Should().BeTrue();
	}

	[Fact]
	public async Task DirtyFlag_TogglesOnApplyAndMarkSaved()
	{
		var harness = await BuildHarnessAsync();

		harness.Session.MarkSaved();
		harness.Session.IsDirty.Should().BeFalse();

		harness.Session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		harness.Session.IsDirty.Should().BeTrue("any successful mutation flips IsDirty");

		harness.Session.MarkSaved();
		harness.Session.IsDirty.Should().BeFalse();
	}

	#endregion

	#region Helpers

	private static async Task<Harness> BuildHarnessAsync()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync("WithGroups");
		return new Harness(services, session);
	}

	private static void AppendWait(Harness harness, float durationSeconds)
	{
		harness.Session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		var lastIndex = harness.Session.Current.StepCount - 1;
		harness.Session.UpdateStepProperty(
				lastIndex,
				DurationColumn,
				durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture))
			.IsSuccess.Should().BeTrue();
	}

	private static Step BuildWaitStep(Harness harness, float durationSeconds)
	{
		var registry = harness.Services.GetRequiredService<RecipeMetadataRegistry>();
		var action = registry.GetAction(WaitActionId).Value;

		var step = StepInitializer.Create(action, registry);
		return step.WithProperty(
			DurationColumn,
			PropertyValue.FromFloat(durationSeconds));
	}

	private static Step BuildPauseStep(Harness harness)
	{
		var registry = harness.Services.GetRequiredService<RecipeMetadataRegistry>();
		var action = registry.GetAction(PauseActionId).Value;

		return StepInitializer.Create(action, registry);
	}

	private sealed record Harness(
		IServiceProvider Services,
		RecipeSession Session);

	#endregion
}
