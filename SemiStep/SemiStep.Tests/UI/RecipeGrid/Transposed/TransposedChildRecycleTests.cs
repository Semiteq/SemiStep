using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Gate for the in-place child recycle that <see cref="TransposedStepListBox"/> enables: a container
/// scrolled out and reused for a DIFFERENT column must keep the SAME item <c>ContentPresenter.Child</c>
/// and the SAME <see cref="TransposedColumnCellsHost"/> instance (no subtree rebuild), while its header
/// step-number text now shows the new column's value (no stale content from the skipped base clear).
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedChildRecycleTests : IAsyncLifetime
{
	private const int SeededStepCount = 40;
	private const double NarrowWindowWidth = 560;

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

	[AvaloniaFact]
	public void Recycle_ReusesContentPresenterChildAndHost_OntoDifferentColumn()
	{
		var stepListBox = ShowView();
		var panel = ColumnsPanel(stepListBox);

		// Snapshot each realized container's OWN item ContentPresenter child (the presenter presenting the
		// ListBoxItem's content, not an arbitrary descendant) and its cells host, keyed by the column the
		// container currently shows, so a real recycle onto a different column can be proven by reference.
		var initialContainers = panel.Children.ToList();
		initialContainers.Should().NotBeEmpty("the panel must realize a viewport of columns on load");

		var snapshots = initialContainers.ToDictionary(
			container => container,
			container => new ContainerSnapshot(
				container.DataContext,
				ItemContentPresenter(container).Child!,
				CellsHost(container)));

		ScrollToHorizontalEnd(stepListBox);

		// Pick a container that is now VISIBLE and bound to a DIFFERENT column: the bounded child count at
		// the far end forces at least one initial container to be reused for a far column.
		var recycled = initialContainers.FirstOrDefault(container =>
			container.IsVisible
			&& container.DataContext is StepColumnViewModel
			&& !ReferenceEquals(container.DataContext, snapshots[container].BoundColumn));

		recycled.Should().NotBeNull(
			"scrolling to the far end must reuse at least one initial container onto a different column");

		var snapshot = snapshots[recycled!];
		var newColumn = (StepColumnViewModel)recycled!.DataContext!;

		ReferenceEquals(ItemContentPresenter(recycled).Child, snapshot.PresenterChild).Should().BeTrue(
			"the item ContentPresenter child subtree must be recycled in place, not rebuilt, across a recycle");
		ReferenceEquals(CellsHost(recycled), snapshot.Host).Should().BeTrue(
			"the TransposedColumnCellsHost instance must survive the recycle (no subtree teardown)");

		HeaderText(recycled).Text.Should().Be(
			newColumn.Row.StepNumber.ToString(CultureInfo.CurrentCulture),
			"the recycled child must re-point to the new column, showing its step number (no stale content)");
	}

	private static ContentPresenter ItemContentPresenter(Control container)
	{
		return container.GetVisualDescendants()
			.OfType<ContentPresenter>()
			.First(presenter => ReferenceEquals(presenter.TemplatedParent, container));
	}

	private static TransposedColumnCellsHost CellsHost(Control container)
	{
		return container.GetVisualDescendants().OfType<TransposedColumnCellsHost>().First();
	}

	private static TextBlock HeaderText(Control container)
	{
		var headerBorder = container.GetVisualDescendants()
			.OfType<Border>()
			.First(border => border.Classes.Contains("transposed-step-header"));

		return headerBorder.GetVisualDescendants().OfType<TextBlock>().First();
	}

	private static TransposedColumnsPanel ColumnsPanel(ListBox stepListBox)
	{
		return stepListBox.GetVisualDescendants().OfType<TransposedColumnsPanel>().Single();
	}

	private static void ScrollToHorizontalEnd(ListBox stepListBox)
	{
		var scrollViewer = stepListBox.GetVisualDescendants().OfType<ScrollViewer>().First();
		scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Extent.Width, 0);
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
			Width = NarrowWindowWidth,
			Height = 800,
			Content = view,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return stepListBox;
	}

	private readonly record struct ContainerSnapshot(
		object? BoundColumn,
		Control PresenterChild,
		TransposedColumnCellsHost Host);
}
