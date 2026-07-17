using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Guards the "add step while scrolled far away" path: the app auto-scrolls to a freshly inserted
/// step, and if the viewport is far from it the horizontal panel realizes a full viewport of columns
/// in one frame. This test asserts a single far <see cref="ItemsControl.ScrollIntoView(int)"/> jump
/// still realizes only a viewport of containers, never the whole recipe — the invariant the
/// allocation-reduction work must preserve. Suite-resident (no SEMISTEP_PROBE gate).
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedViewportJumpTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateTransposedSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	// A recycled panel realizes essentially one viewport of columns after the jump; bound the post-jump
	// count to a small multiple of the initially-realized count (not the loose SeededStepCount/2 = 20,
	// which a regression realizing 15-19 columns would pass) so genuine over-realization is caught.
	private const int RecycleSlackFactor = 2;

	[AvaloniaFact]
	public void FarJumpToLastColumn_RealizedContainerCount_StaysViewportBound()
	{
		var stepListBox = ShowView();

		var initialCount = RealizedContainerCount(stepListBox);
		initialCount.Should().BeLessThan(
			SeededStepCount / 2, "the panel must realize a viewport of columns, not the recipe");

		JumpToLastColumn(stepListBox);

		RealizedContainerCount(stepListBox).Should().BeLessThanOrEqualTo(
			initialCount * RecycleSlackFactor,
			"a far ScrollIntoView jump must recycle into a viewport of containers, not accumulate the recipe");
	}

	[AvaloniaFact]
	public void FarJumpThenJumpBack_RealizedContainerCount_StaysViewportBound()
	{
		var stepListBox = ShowView();
		var initialCount = RealizedContainerCount(stepListBox);

		JumpToLastColumn(stepListBox);
		JumpToColumn(stepListBox, 0);

		RealizedContainerCount(stepListBox).Should().BeLessThanOrEqualTo(
			initialCount * RecycleSlackFactor, "jumping back must recycle containers, not accumulate them");
	}

	private static int RealizedContainerCount(ListBox stepListBox)
	{
		return stepListBox.GetRealizedContainers().Count();
	}

	private void JumpToLastColumn(ListBox stepListBox)
	{
		JumpToColumn(stepListBox, _surface.StepColumns.Count - 1);
	}

	private static void JumpToColumn(ListBox stepListBox, int index)
	{
		stepListBox.ScrollIntoView(index);
		Dispatcher.UIThread.RunJobs();
	}

	private ListBox ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window = new Window
		{
			Width = 560,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}
}
