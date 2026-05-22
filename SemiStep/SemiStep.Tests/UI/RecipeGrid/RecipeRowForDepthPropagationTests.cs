using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeRowForDepthPropagationTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private RecipeGridViewModel _grid = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_grid = new RecipeGridViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			_fixture.MessagePanel,
			NullLogger<RecipeGridViewModel>.Instance);
		_fixture.Coordinator.Mutated += _grid.OnMutation;
		_grid.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_grid.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void NestedForLoops_PropagateForDepthAndDerivedFlags()
	{
		_fixture.Coordinator.NewRecipe();
		AppendFor(1);
		AppendWait();
		AppendFor(2);
		AppendWait();
		AppendEndFor();
		AppendEndFor();

		_grid.RecipeRows.Should().HaveCount(6);

		AssertForDepth(0, 1);
		AssertForDepth(1, 1);
		AssertForDepth(2, 2);
		AssertForDepth(3, 2);
		AssertForDepth(4, 2);
		AssertForDepth(5, 1);
	}

	[AvaloniaFact]
	public void NoLoops_AllRowsHaveDepthZero()
	{
		_fixture.Coordinator.NewRecipe();
		AppendWait();
		AppendWait();
		AppendWait();

		foreach (var row in _grid.RecipeRows)
		{
			row.ForDepth.Should().Be(0);
			row.IsForDepth1.Should().BeFalse();
			row.IsForDepth2.Should().BeFalse();
			row.IsForDepth3.Should().BeFalse();
		}
	}

	[AvaloniaFact]
	public void MaximumNestedLoops_ClampedAtDepth3InUiLayer()
	{
		// Tint convention: the parser reports loop depth as `stack.Count + 1` (1-based), and
		// `RecipeSnapshot.RowLoopDepths` uses that depth verbatim, so a row inside N nested
		// loops carries tint N. The UI layer caps the propagated value at 3. This recipe nests
		// three For-loops; the innermost row therefore receives natural tint 3 (no clamping
		// occurs because the natural value already equals the cap).
		_fixture.Coordinator.NewRecipe();
		AppendFor(1);
		AppendFor(1);
		AppendFor(1);
		AppendWait();
		AppendEndFor();
		AppendEndFor();
		AppendEndFor();

		var innermostWait = _grid.RecipeRows[3];
		innermostWait.ForDepth.Should().Be(3);
		innermostWait.IsForDepth1.Should().BeFalse();
		innermostWait.IsForDepth2.Should().BeFalse();
		innermostWait.IsForDepth3.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ForDepthChange_RaisesInpcForIsForDepthFlags()
	{
		_fixture.Coordinator.NewRecipe();
		AppendWait();

		var row = _grid.RecipeRows[0];
		var changedProperties = new List<string?>();
		row.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		row.ForDepth = 2;

		changedProperties.Should().Contain(nameof(RecipeRowViewModel.IsForDepth2));
	}

	private void AppendFor(int iterations)
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);
		var index = _fixture.Coordinator.CurrentRecipe.StepCount - 1;
		_fixture.Coordinator.UpdateStepProperty(index, RecipeTestDriver.TaskColumn, ((float)iterations).ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	private void AppendEndFor()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.EndForLoopActionId);
	}

	private void AppendWait()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var index = _fixture.Coordinator.CurrentRecipe.StepCount - 1;
		_fixture.Coordinator.UpdateStepProperty(index, RecipeTestDriver.StepDurationColumn, "10");
	}

	private void AssertForDepth(int rowIndex, int expectedDepth)
	{
		var row = _grid.RecipeRows[rowIndex];
		row.ForDepth.Should().Be(expectedDepth);
		row.IsForDepth1.Should().Be(expectedDepth == 1);
		row.IsForDepth2.Should().Be(expectedDepth == 2);
		row.IsForDepth3.Should().Be(expectedDepth >= 3);
	}
}
