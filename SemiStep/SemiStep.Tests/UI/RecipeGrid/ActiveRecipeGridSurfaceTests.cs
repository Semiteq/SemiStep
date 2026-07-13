using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class ActiveRecipeGridSurfaceTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;

	private readonly UIFixture _fixture = new();
	private ActiveRecipeGridSurface _router = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_router = _fixture.CreateActiveSurface();
	}

	public async ValueTask DisposeAsync()
	{
		_router.CanonicalSurface.Dispose();
		_router.TransposedSurface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void StartupOrientation_DefaultsToCanonical()
	{
		_router.Orientation.Should().Be(GridOrientation.RowsAsSteps);
	}

	[AvaloniaFact]
	public void StartupOrientation_ColumnsAsSteps_StartsTransposed()
	{
		var configuredStyle = _fixture.AppConfiguration.GridStyle with
		{
			Orientation = GridOrientation.ColumnsAsSteps
		};
		var router = _fixture.CreateActiveSurface(configuredStyle);

		try
		{
			router.Orientation.Should().Be(GridOrientation.ColumnsAsSteps);

			router.Initialize();
			router.StepCount.Should().Be(
				SeededStepCount, "the transposed surface must be active from startup");
		}
		finally
		{
			router.CanonicalSurface.Dispose();
			router.TransposedSurface.Dispose();
		}
	}

	[AvaloniaFact]
	public void Initialize_FansOutToBothSurfaces()
	{
		_router.Initialize();

		_router.CanonicalSurface.StepCount.Should().Be(SeededStepCount);
		_router.TransposedSurface.StepCount.Should().Be(SeededStepCount);

		_router.ToggleOrientation();

		_router.StepCount.Should().Be(
			SeededStepCount, "the newly active surface must already carry the projection");
	}

	[AvaloniaFact]
	public void ToggleOrientation_FlipsBackAndForth()
	{
		_router.ToggleOrientation();
		_router.Orientation.Should().Be(GridOrientation.ColumnsAsSteps);

		_router.ToggleOrientation();
		_router.Orientation.Should().Be(GridOrientation.RowsAsSteps);
	}

	[AvaloniaFact]
	public void Delegation_TracksActiveSurface()
	{
		_router.Initialize();

		_router.UpdateSelection([2]);
		_router.CanonicalSurface.SelectedStepIndices.Should().Equal(2);
		_router.TransposedSurface.SelectedStepIndices.Should().BeEmpty();

		_router.ToggleOrientation();
		_router.UpdateSelection([0, 1]);

		_router.TransposedSurface.SelectedStepIndices.Should().Equal(0, 1);
		_router.SelectedStepIndex.Should().Be(0);
		_router.CollectSelectedSteps().Should().HaveCount(2);
	}

	[AvaloniaFact]
	public void ToggleOrientation_TransfersSelectionToIncomingSurface()
	{
		_router.Initialize();
		_router.UpdateSelection([1]);

		_router.ToggleOrientation();

		_router.TransposedSurface.SelectedStepIndices.Should().Equal(1);
		_router.SelectedStepIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void ToggleOrientation_TransfersMultiSelectionToIncomingSurface()
	{
		_router.Initialize();
		_router.UpdateSelection([0, 2]);

		_router.ToggleOrientation();

		_router.TransposedSurface.SelectedStepIndices.Should().Equal(0, 2);
		_router.CollectSelectedSteps().Should().HaveCount(2);
	}

	[AvaloniaFact]
	public void EditInTransposed_PersistsToCanonical_AndSelectionSurvivesRoundTrip()
	{
		_router.Initialize();
		_router.ToggleOrientation();
		_router.UpdateSelection([1]);

		_router.TransposedSurface.StepColumns[0].Row[RecipeTestDriver.StepDurationColumn] = "45";

		_router.CanonicalSurface.RecipeRows[0][RecipeTestDriver.StepDurationColumn].Should().Be(45f);

		_router.ToggleOrientation();
		_router.SelectedStepIndex.Should().Be(1, "selection must survive the flip back to canonical");
		_router.CanonicalSurface.SelectedStepIndices.Should().Equal(1);

		_router.ToggleOrientation();
		_router.SelectedStepIndex.Should().Be(1, "selection must survive the flip back to transposed");
	}

	[AvaloniaFact]
	public void CanDeleteStep_SubscribersKeepReceivingValuesAcrossSwap()
	{
		_router.Initialize();

		var observedValues = new List<bool>();
		using var subscription = _router.CanDeleteStep.Subscribe(observedValues.Add);

		observedValues.Should().Equal(false);

		_router.UpdateSelection([0]);
		observedValues.Should().Equal(false, true);

		_router.ToggleOrientation();
		observedValues.Should().Equal(
			[false, true], "the carried-over selection keeps the value stable across the swap");

		_router.UpdateSelection([]);
		observedValues.Should().Equal(false, true, false);
	}

	[AvaloniaFact]
	public void SelectionRequests_ReEmitsFromActiveSurface_AcrossSwaps()
	{
		_router.Initialize();

		var observedRequests = new List<int?>();
		using var subscription = _router.SelectionRequests.Subscribe(observedRequests.Add);

		_router.RequestSelection(1);
		observedRequests.Should().Equal(1);

		_router.ToggleOrientation();
		_router.RequestSelection(2);
		observedRequests.Should().Equal(1, 2);

		_router.CanonicalSurface.RequestSelection(0);
		observedRequests.Should().Equal(
			[1, 2], "requests on the inactive surface must not leak through the router");
	}

	[AvaloniaFact]
	public void EditorMustClose_ReEmitsFromActiveSurface_AcrossSwaps()
	{
		_router.Initialize();
		_router.ToggleOrientation();

		var editorMustCloseCount = 0;
		using var subscription = _router.EditorMustClose.Subscribe(_ => editorMustCloseCount++);

		_fixture.SetRecipeActive(true);

		editorMustCloseCount.Should().Be(1);
		_router.IsReadOnly.Should().BeTrue();
	}
}
