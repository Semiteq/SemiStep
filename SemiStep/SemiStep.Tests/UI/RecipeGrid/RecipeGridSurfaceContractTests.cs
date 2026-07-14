using System.Globalization;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>
/// Contract every <see cref="IRecipeGridSurface"/> implementation must satisfy.
/// Concrete fixtures derive from this class and supply the surface under test;
/// the fixture recipe is seeded with four steps before the surface is created.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public abstract class RecipeGridSurfaceContractTests : IAsyncLifetime
{
	private const int SeededStepCount = 4;

	protected UIFixture Fixture { get; } = new();

	protected IRecipeGridSurface Surface { get; private set; } = null!;

	protected abstract IRecipeGridSurface CreateSurface(UIFixture fixture);

	// The base holds only IRecipeGridSurface, which exposes no per-row accessor, and the two
	// surfaces reach their rows differently. Each fixture overrides this so the row-level
	// start-time/ForDepth assertions run uniformly against both orientations.
	protected abstract RecipeRowViewModel RowAt(int index);

	public async ValueTask InitializeAsync()
	{
		await Fixture.InitializeAsync();
		Fixture.SeedRecipe(SeededStepCount);

		Surface = CreateSurface(Fixture);
		Surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		Surface.Dispose();
		await Fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Initialize_ProjectsSeededRecipe_StepCountMatches()
	{
		Surface.StepCount.Should().Be(SeededStepCount);
	}

	[AvaloniaFact]
	public void IsReadOnly_TracksCoordinatorCanEditRecipe()
	{
		Surface.IsReadOnly.Should().BeFalse();

		Fixture.SetRecipeActive(true);
		Surface.IsReadOnly.Should().BeTrue();

		Fixture.SetRecipeActive(false);
		Surface.IsReadOnly.Should().BeFalse();
	}

	[AvaloniaFact]
	public void UpdateSelection_WithIndices_ExposesSelection()
	{
		Surface.UpdateSelection([1, 3]);

		Surface.SelectedStepIndices.Should().Equal(1, 3);
		Surface.SelectedStepIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void UpdateSelection_WithEmptyList_ClearsSelection()
	{
		Surface.UpdateSelection([1, 3]);

		Surface.UpdateSelection([]);

		Surface.SelectedStepIndices.Should().BeEmpty();
		Surface.SelectedStepIndex.Should().Be(-1);
	}

	[AvaloniaFact]
	public void RequestSelection_WithIndex_EmitsOnSelectionRequests()
	{
		int? received = -100;
		using var subscription = Surface.SelectionRequests.Subscribe(index => received = index);

		Surface.RequestSelection(2);

		received.Should().Be(2);
	}

	[AvaloniaFact]
	public void RequestSelection_WithNull_EmitsNullOnSelectionRequests()
	{
		int? received = -100;
		var emitted = false;
		using var subscription = Surface.SelectionRequests.Subscribe(index =>
		{
			received = index;
			emitted = true;
		});

		Surface.RequestSelection(null);

		emitted.Should().BeTrue();
		received.Should().BeNull();
	}

	[AvaloniaFact]
	public void EditorMustClose_EmitsOnReadOnlyTransition_NotOnRelease()
	{
		var emissionCount = 0;
		using var subscription = Surface.EditorMustClose.Subscribe(_ => emissionCount++);

		Fixture.SetRecipeActive(true);
		emissionCount.Should().Be(1);

		Fixture.SetRecipeActive(false);
		emissionCount.Should().Be(1);
	}

	[AvaloniaFact]
	public void EditorMustClose_SubscribedWhileAlreadyReadOnly_DoesNotReplay()
	{
		Fixture.SetRecipeActive(true);

		var emissionCount = 0;
		using var subscription = Surface.EditorMustClose.Subscribe(_ => emissionCount++);

		emissionCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void CanDeleteStep_TrueIffSelectionNonEmpty_ReactsToUpdateSelection()
	{
		var values = new List<bool>();
		using var subscription = Surface.CanDeleteStep.Subscribe(values.Add);

		values.Should().Equal(false);

		Surface.UpdateSelection([0]);
		values[^1].Should().BeTrue();

		Surface.UpdateSelection([]);
		values[^1].Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanDeleteStep_UnchangedValue_DoesNotReEmit()
	{
		var emissionCount = 0;
		using var subscription = Surface.CanDeleteStep.Subscribe(_ => emissionCount++);
		emissionCount.Should().Be(1);

		Surface.UpdateSelection([0]);
		emissionCount.Should().Be(2);

		Surface.UpdateSelection([1]);
		emissionCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void CollectSelectedSteps_ReturnsStepsInAscendingIndexOrder()
	{
		Surface.UpdateSelection([2, 0]);

		var steps = Surface.CollectSelectedSteps();

		var recipe = Fixture.Coordinator.CurrentRecipe;
		steps.Should().HaveCount(Surface.SelectedStepIndices.Count);
		steps[0].Should().Be(recipe.Steps[0]);
		steps[1].Should().Be(recipe.Steps[2]);
	}

	[AvaloniaFact]
	public void Dispose_CoordinatorSignals_ProduceNoFurtherEmissionsOrStateChanges()
	{
		Surface.UpdateSelection([1]);
		var stepCountBeforeDispose = Surface.StepCount;

		var canDeleteEmissions = 0;
		var editorMustCloseEmissions = 0;
		using var canDeleteSubscription = Surface.CanDeleteStep.Subscribe(_ => canDeleteEmissions++);
		using var editorMustCloseSubscription = Surface.EditorMustClose.Subscribe(_ => editorMustCloseEmissions++);
		var canDeleteBaseline = canDeleteEmissions;

		Surface.Dispose();

		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		Fixture.SetRecipeActive(true);

		canDeleteEmissions.Should().Be(canDeleteBaseline);
		editorMustCloseEmissions.Should().Be(0);
		Surface.StepCount.Should().Be(stepCountBeforeDispose);
		Surface.IsReadOnly.Should().BeFalse();
		Surface.SelectedStepIndices.Should().Equal(1);
	}

	[AvaloniaFact]
	public void Dispose_ConsumerFacingCalls_AreSafeNoOps()
	{
		Surface.Dispose();

		var requestExisting = () => Surface.RequestSelection(0);
		var requestClear = () => Surface.RequestSelection(null);
		var updateSelection = () => Surface.UpdateSelection([]);

		requestExisting.Should().NotThrow();
		requestClear.Should().NotThrow();
		updateSelection.Should().NotThrow();
	}

	[AvaloniaFact]
	public void Initialize_PopulatesStartTimeAndDepth_ForEveryRowIncludingFirst()
	{
		// Distinct per-step durations give each row a distinct cumulative start-time (0, 10, 30, 60),
		// so a baseline that populated rows off a wrong per-row index would read a different value and
		// fail here. A bare-Wait seed (every start-time "0s") would hide that.
		SeedDistinctDurations(4);

		// Re-initialize a fresh surface so the assertions exercise the Initialize baseline itself,
		// not the incremental OnMutation tail that seeded the rows during SeedDistinctDurations.
		Surface.Dispose();
		Surface = CreateSurface(Fixture);
		Surface.Initialize();

		Surface.StepCount.Should().Be(4);

		for (var i = 0; i < Surface.StepCount; i++)
		{
			RowAt(i).StepStartTime.Should().NotBeNullOrEmpty();
			RowAt(i).StepStartTime.Should().Be(ExpectedStartTime(i));
			RowAt(i).ForDepth.Should().Be(ExpectedDepth(i));
		}
	}

	[AvaloniaFact]
	public void Append_KeepsEarlierStartTimes_AndPopulatesNewRow()
	{
		SeedDistinctDurations(4);
		var before = CaptureStartTimes(Surface.StepCount);

		AppendWait(50f);

		for (var i = 0; i < before.Count; i++)
		{
			RowAt(i).StepStartTime.Should().Be(before[i]);
		}

		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void Insert_KeepsRowsBeforeInsertion_AndRefreshesTail()
	{
		SeedDistinctDurations(4);
		var insertIndex = 2;
		var before = CaptureStartTimes(insertIndex);

		Fixture.Coordinator.InsertStep(insertIndex, RecipeTestDriver.WaitActionId);
		Fixture.Coordinator.UpdateStepProperty(
			insertIndex, RecipeTestDriver.StepDurationColumn, "5");

		for (var i = 0; i < insertIndex; i++)
		{
			RowAt(i).StepStartTime.Should().Be(before[i]);
		}

		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void RemoveSingle_KeepsRowsBeforeRemoval_AndRefreshesTail()
	{
		SeedDistinctDurations(4);
		var removeIndex = 1;
		var before = CaptureStartTimes(removeIndex);

		Fixture.Coordinator.RemoveStep(removeIndex);

		for (var i = 0; i < removeIndex; i++)
		{
			RowAt(i).StepStartTime.Should().Be(before[i]);
		}

		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void RemoveMultiple_KeepsRowsBeforeFirstRemoval_AndRefreshesTail()
	{
		SeedDistinctDurations(5);
		var before = CaptureStartTimes(1);

		Fixture.Coordinator.RemoveSteps(new[] { 1, 3 });

		RowAt(0).StepStartTime.Should().Be(before[0]);
		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void PropertyUpdate_MiddleStepDuration_KeepsUpstream_RefreshesDownstream()
	{
		SeedDistinctDurations(5);
		var editIndex = 2;
		var before = CaptureStartTimes(editIndex + 1);

		Fixture.Coordinator.UpdateStepProperty(
			editIndex, RecipeTestDriver.StepDurationColumn, "999");

		// A duration edit at editIndex shifts every downstream first-arrival time but leaves the
		// step's own start-time and all upstream rows untouched (start-time[i] depends on 0..i-1).
		for (var i = 0; i <= editIndex; i++)
		{
			RowAt(i).StepStartTime.Should().Be(before[i]);
		}

		RowAt(editIndex + 1).StepStartTime.Should().NotBe(before[editIndex]);
		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void RemoveAtIndexZero_RefreshesEntireTail()
	{
		SeedDistinctDurations(4);

		Fixture.Coordinator.RemoveStep(0);

		// refreshFrom = 0: every surviving row shifts up and must be re-derived from the snapshot.
		Surface.StepCount.Should().Be(3);
		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void AppendToSingleStepRecipe_PopulatesBothRows()
	{
		Fixture.Coordinator.NewRecipe();
		AppendWait(15f);

		Surface.StepCount.Should().Be(1);
		RowAt(0).StepStartTime.Should().Be(ExpectedStartTime(0));

		AppendWait(25f);

		Surface.StepCount.Should().Be(2);
		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void ActionChange_InsideLoop_RebuiltRowRepopulatesStartTimeAndDepth()
	{
		Fixture.Coordinator.NewRecipe();
		AppendFor(2);
		AppendWait(10f);
		AppendWait(20f);
		AppendEndFor();

		var changeIndex = 2;
		RowAt(changeIndex).ForDepth.Should().Be(1);

		Fixture.Coordinator.ChangeStepAction(changeIndex, RecipeTestDriver.PauseActionId);

		// RebuildItem installs a fresh row (StepStartTime=null, ForDepth=0); the tail must
		// repopulate both. refreshFrom = changeIndex covers the start-time; the full-scan
		// depth refresh restores the loop membership.
		RowAt(changeIndex).StepStartTime.Should().NotBeNullOrEmpty();
		RowAt(changeIndex).StepStartTime.Should().Be(ExpectedStartTime(changeIndex));
		RowAt(changeIndex).ForDepth.Should().Be(1);
		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void RecipeReplace_RefreshesEveryRowFromZero()
	{
		SeedDistinctDurations(4);
		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		// Undo dispatches RecipeReplaced.
		Fixture.Coordinator.Undo();

		for (var i = 0; i < Surface.StepCount; i++)
		{
			RowAt(i).StepStartTime.Should().NotBeNullOrEmpty();
		}

		AssertStartTimesConsistent();
	}

	[AvaloniaFact]
	public void RemoveEndForLoop_DropsDepthOfRowsAboveIt()
	{
		Fixture.Coordinator.NewRecipe();
		AppendFor(2);
		AppendWait(10f);
		AppendWait(10f);
		AppendEndFor();
		AppendWait(10f);

		RowAt(0).ForDepth.Should().Be(1);
		RowAt(1).ForDepth.Should().Be(1);
		RowAt(2).ForDepth.Should().Be(1);

		// Removing the EndForLoop unbalances the loop: LoopParser warns and forms no loop, so the
		// rows above the removal must drop to depth 0. An incremental depth refresh from the
		// removal index would leave them stale at depth 1; this pins the full-scan depth decision.
		Fixture.Coordinator.RemoveStep(3);

		RowAt(0).ForDepth.Should().Be(0);
		RowAt(1).ForDepth.Should().Be(0);
		RowAt(2).ForDepth.Should().Be(0);
	}

	[AvaloniaFact]
	public void AppendStep_PerSurfaceTailAllocation_DoesNotScaleWithRecipeSize()
	{
		var smallRecipeSize = 8;
		var largeRecipeSize = 512;
		var smallDelta = MeasureSecondSurfaceAppendDelta(smallRecipeSize);
		var largeDelta = MeasureSecondSurfaceAppendDelta(largeRecipeSize);

		// A single Core re-analysis runs per mutation regardless of how many surfaces subscribe, so the
		// extra allocation a second subscribed surface adds to one append is that surface's OnMutation
		// tail: a fresh row view model (size-independent) plus the start-time refresh. The constant
		// row-VM term cancels in largeDelta - smallDelta, leaving only the start-time refresh's growth.
		//
		// Incremental refresh formats one row at any recipe size, so the growth is ~0 (measured
		// deterministically negative: the large sample allocates no more than the small one). A reverted
		// full-scan tail re-formats every existing row, so the growth jumps to ~(512 - 8) row-formats --
		// measured ~57 KB and up. The absolute ceiling sits an order of magnitude above the incremental
		// floor and well below the full-scan regression, so the guard fails loudly on a reverted full
		// scan (>= ~7x the budget) yet never flakes on the incremental tail. An absolute ceiling, not a
		// ratio, because the constant row-VM term would otherwise dilute a same-surface ratio.
		var perSurfaceTailGrowthBudgetBytes = 8 * 1024;
		var growth = largeDelta - smallDelta;

		growth.Should().BeLessThan(
			perSurfaceTailGrowthBudgetBytes,
			"per-surface append tail must not grow with recipe size (was small={0}, large={1})",
			smallDelta,
			largeDelta);
	}

	// Extra allocation one additional subscribed surface adds to a single append. Both the one- and
	// two-surface appends run consecutively on the same JIT-warm recipe (two rows apart), so the large,
	// reproducible per-mutation Core re-analysis cancels in the difference and only its two-row residual
	// survives -- negligible next to the tail growth this guard watches. The difference is therefore the
	// second surface's OnMutation tail: a constant fresh row view model plus the start-time refresh.
	private long MeasureSecondSurfaceAppendDelta(int seedCount)
	{
		Fixture.Coordinator.NewRecipe();
		for (var i = 0; i < seedCount; i++)
		{
			Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		}

		// Warm the single-surface path, then measure one append with only the fixture's surface.
		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var oneSurfaceAllocation = MeasureAppendAllocation();

		var secondSurface = CreateSurface(Fixture);
		secondSurface.Initialize();

		// Warm the two-surface path, then measure one append with both surfaces subscribed.
		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var twoSurfaceAllocation = MeasureAppendAllocation();

		secondSurface.Dispose();

		return twoSurfaceAllocation - oneSurfaceAllocation;
	}

	private long MeasureAppendAllocation()
	{
		var before = GC.GetAllocatedBytesForCurrentThread();
		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var after = GC.GetAllocatedBytesForCurrentThread();

		return after - before;
	}

	private void SeedDistinctDurations(int count)
	{
		Fixture.Coordinator.NewRecipe();
		for (var i = 0; i < count; i++)
		{
			AppendWait((i + 1) * 10f);
		}
	}

	private void AppendWait(float durationSeconds)
	{
		Fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var index = Fixture.Coordinator.CurrentRecipe.StepCount - 1;
		Fixture.Coordinator.UpdateStepProperty(
			index,
			RecipeTestDriver.StepDurationColumn,
			durationSeconds.ToString(CultureInfo.InvariantCulture));
	}

	private void AppendFor(int iterations)
	{
		Fixture.Coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);
		var index = Fixture.Coordinator.CurrentRecipe.StepCount - 1;
		Fixture.Coordinator.UpdateStepProperty(
			index,
			RecipeTestDriver.TaskColumn,
			((float)iterations).ToString(CultureInfo.InvariantCulture));
	}

	private void AppendEndFor()
	{
		Fixture.Coordinator.AppendStep(RecipeTestDriver.EndForLoopActionId);
	}

	private IReadOnlyList<string?> CaptureStartTimes(int count)
	{
		var captured = new string?[count];
		for (var i = 0; i < count; i++)
		{
			captured[i] = RowAt(i).StepStartTime;
		}

		return captured;
	}

	private void AssertStartTimesConsistent()
	{
		for (var i = 0; i < Surface.StepCount; i++)
		{
			RowAt(i).StepStartTime.Should().Be(
				ExpectedStartTime(i),
				"row {0} start-time must match the snapshot after the mutation",
				i);
		}
	}

	// Mirrors RecipeGridSurfaceBase.RefreshStepStartTimes exactly, so it is a faithful oracle for
	// the value a fully-refreshed row must carry.
	private string ExpectedStartTime(int index)
	{
		var stepStartTimes = Fixture.Coordinator.Snapshot.StepStartTimes;
		if (!stepStartTimes.TryGetValue(index, out var time))
		{
			return string.Empty;
		}

		var rawSeconds = time.TotalSeconds.ToString(CultureInfo.InvariantCulture);
		return TimeFormatHelper.FormatValue(
			rawSeconds,
			TimeFormatHelper.TimeHmsFormat,
			TimeFormatHelper.TimeUnits);
	}

	private int ExpectedDepth(int index)
	{
		return Math.Min(Fixture.Coordinator.Snapshot.RowLoopDepths[index], 3);
	}
}
