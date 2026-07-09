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
}
