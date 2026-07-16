using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedRecipeGridViewTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;
	private const string CellClass = "transposed-cell";
	private const string MarkerClass = "transposed-current-marker";

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
	public void View_WithThreeStepSurface_RendersColumnPerStep_AndParameterNameColumn()
	{
		var (view, stepListBox) = ShowView();

		var stepContainers = stepListBox.GetVisualDescendants().OfType<ListBoxItem>().ToList();
		stepContainers.Should().HaveCount(SeededStepCount);

		var parameterNameColumn = view.FindControl<ItemsControl>("ParameterNameColumn");
		parameterNameColumn.Should().NotBeNull();
		parameterNameColumn!.ItemCount.Should().Be(_surface.ParameterDescriptors.Count);
	}

	[AvaloniaFact]
	public void ClickOnSecondColumnHeader_SelectsStepOne()
	{
		var (_, stepListBox) = ShowView();

		var window = _window!;
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(1)!;
		var clickPoint = container.TranslatePoint(new Point(5, 5), window);
		clickPoint.Should().NotBeNull();

		window.MouseDown(clickPoint!.Value, MouseButton.Left);
		window.MouseUp(clickPoint.Value, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();

		container.IsSelected.Should().BeTrue();
		_surface.SelectedStepIndex.Should().Be(1);
		_surface.SelectedStepIndices.Should().Equal(1);
	}

	[AvaloniaFact]
	public void SelectionRequest_OnSurface_SelectsCorrespondingColumn()
	{
		var (_, stepListBox) = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		stepListBox.SelectedIndex.Should().Be(1);
		stepListBox.SelectedItem.Should().BeSameAs(_surface.StepColumns[1]);
	}

	[AvaloniaFact]
	public void SelectionRequest_WithNull_ClearsSelection()
	{
		var (_, stepListBox) = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		_surface.RequestSelection(null);
		Dispatcher.UIThread.RunJobs();

		stepListBox.SelectedIndex.Should().Be(-1);
		_surface.SelectedStepIndices.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void SelectionRequest_OutOfRange_LeavesSelectionUnchanged()
	{
		var (_, stepListBox) = ShowView();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		_surface.RequestSelection(99);
		Dispatcher.UIThread.RunJobs();

		stepListBox.SelectedIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void MultiSelection_AddedInReverseOrder_PropagatesAscendingStepIndices()
	{
		var (_, stepListBox) = ShowView();

		stepListBox.SelectedItems!.Add(_surface.StepColumns[2]);
		Dispatcher.UIThread.RunJobs();
		stepListBox.SelectedItems!.Add(_surface.StepColumns[0]);
		Dispatcher.UIThread.RunJobs();

		_surface.SelectedStepIndices.Should().Equal(0, 2);
		_surface.SelectedStepIndex.Should().Be(0);
	}

	[AvaloniaFact]
	public void CurrentStep_StampsClassOnContainer_AndShowsMarker()
	{
		var (_, stepListBox) = ShowView();

		_surface.StepColumns[1].Row.IsCurrentStep = true;
		Dispatcher.UIThread.RunJobs();

		var currentContainer = (ListBoxItem)stepListBox.ContainerFromIndex(1)!;
		currentContainer.Classes.Contains(RowExecutionClasses.CurrentStepClass).Should().BeTrue();
		FindMarker(currentContainer).IsVisible.Should().BeTrue();

		var idleContainer = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		idleContainer.Classes.Contains(RowExecutionClasses.CurrentStepClass).Should().BeFalse();
		FindMarker(idleContainer).IsVisible.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ForDepth_Toggle_SwapsDepthClassOnContainer()
	{
		var (_, stepListBox) = ShowView();
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(1)!;

		_surface.StepColumns[1].Row.ForDepth = 1;
		Dispatcher.UIThread.RunJobs();

		container.Classes.Contains(RowExecutionClasses.ForDepth1Class).Should().BeTrue();

		_surface.StepColumns[1].Row.ForDepth = 2;
		Dispatcher.UIThread.RunJobs();

		container.Classes.Contains(RowExecutionClasses.ForDepth1Class).Should().BeFalse();
		container.Classes.Contains(RowExecutionClasses.ForDepth2Class).Should().BeTrue();
	}

	[AvaloniaFact]
	public void CellBorders_CarryReadOnlyInapplicableAndChangedClasses()
	{
		var (_, stepListBox) = ShowView();

		var container = (ListBoxItem)stepListBox.ContainerFromIndex(0)!;
		var cellBorders = FindCellBorders(container);
		cellBorders.Should().HaveCount(_surface.ParameterDescriptors.Count);

		var row = _surface.StepColumns[0].Row;
		var descriptors = _surface.ParameterDescriptors;

		for (var i = 0; i < descriptors.Count; i++)
		{
			cellBorders[i].Classes.Contains("read-only-cell").Should().Be(
				descriptors[i].IsReadOnlyParameter,
				"the read-only cell class must mirror the descriptor's read-only flag");
		}

		var inapplicableIndex = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && !row.IsApplicable(descriptor.ParameterKey));
		cellBorders[inapplicableIndex].Classes.Contains("inapplicable").Should().BeTrue();

		var applicableIndex = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));
		cellBorders[applicableIndex].Classes.Contains("inapplicable").Should().BeFalse();

		var changedKey = descriptors[applicableIndex].ParameterKey;
		row.MarkChanged([changedKey]);
		Dispatcher.UIThread.RunJobs();

		cellBorders[applicableIndex].Classes.Contains("changed").Should().BeTrue();

		row.ClearChanged(changedKey);
		Dispatcher.UIThread.RunJobs();

		cellBorders[applicableIndex].Classes.Contains("changed").Should().BeFalse();
	}

	private int IndexOfDescriptor(Func<ParameterDescriptor, bool> predicate)
	{
		var descriptors = _surface.ParameterDescriptors;
		for (var i = 0; i < descriptors.Count; i++)
		{
			if (predicate(descriptors[i]))
			{
				return i;
			}
		}

		throw new InvalidOperationException("No parameter descriptor matches the test predicate.");
	}

	private static Border FindMarker(ListBoxItem container)
	{
		return container.GetVisualDescendants()
			.OfType<Border>()
			.Single(border => border.Classes.Contains(MarkerClass));
	}

	private static List<Border> FindCellBorders(ListBoxItem container)
	{
		return container.GetVisualDescendants()
			.OfType<Border>()
			.Where(border => border.Classes.Contains(CellClass))
			.ToList();
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 800,
			Content = view,
		};

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
