using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;

using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.Tests.Performance.Harness;

// Drives the real transposed view over its public surface: ScrollIntoView on the StepListBox, the
// ListBox selection model, and the coordinator mutation commands.
public sealed class TransposedGridDriver : IRecipeGridDriver
{
	private const int DefaultWindowWidth = 560;
	private const int DefaultWindowHeight = 800;

	private readonly UIFixture _fixture;
	private readonly TransposedRecipeGridSurface _surface;
	private readonly TransposedStepListBox _stepListBox;
	private readonly Window _window;
	private readonly Visual _snapshotScope;

	private TransposedGridDriver(
		UIFixture fixture,
		TransposedRecipeGridSurface surface,
		TransposedStepListBox stepListBox,
		Window window,
		Visual snapshotScope)
	{
		_fixture = fixture;
		_surface = surface;
		_stepListBox = stepListBox;
		_window = window;
		_snapshotScope = snapshotScope;
	}

	public TopLevel Root => _window;

	public Visual SnapshotScope => _snapshotScope;

	public int ItemCount => _surface.StepColumns.Count;

	public IReadOnlyList<int> SelectedIndices => _surface.SelectedStepIndices;

	public IReadOnlyList<int> RealizedIndices
	{
		get
		{
			var containers = _stepListBox.GetRealizedContainers() ?? Enumerable.Empty<Control>();
			return containers
				.Select(container => container.DataContext)
				.OfType<StepColumnViewModel>()
				.Select(column => _surface.StepColumns.IndexOf(column))
				.Where(index => index >= 0)
				.OrderBy(index => index)
				.ToList();
		}
	}

	public IReadOnlyList<Control> RealizedContainers =>
		(_stepListBox.GetRealizedContainers() ?? Enumerable.Empty<Control>()).ToList();

	// The index-based selection model the transposed view feeds. Exposed so the selection-cost probe can
	// toggle a single index inside its stopwatch window without pumping the dispatcher (index-based, so no
	// O(N) item->index lookup lands in the timed region).
	public ISelectionModel Selection => _stepListBox.Selection;

	public static async Task<TransposedGridDriver> CreateAsync(
		string configName = "WithGroups",
		int stepCount = 60,
		int windowWidth = DefaultWindowWidth,
		int windowHeight = DefaultWindowHeight)
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(configName);
		fixture.SeedRecipe(stepCount);

		var surface = fixture.CreateTransposedSurface();
		surface.Initialize();

		var view = new TransposedRecipeGridView { DataContext = surface };
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")
			?? throw new InvalidOperationException(
				"TransposedRecipeGridView is missing its StepListBox anchor.");
		stepListBox.UseTransposedColumnsPanel();

		var window = new Window { Width = windowWidth, Height = windowHeight, Content = view };
		window.Show();
		Dispatcher.UIThread.RunJobs();

		var snapshotScope = ResolveItemsPanel(stepListBox);

		return new TransposedGridDriver(fixture, surface, stepListBox, window, snapshotScope);
	}

	public Task ScrollToColumnAsync(int index)
	{
		_stepListBox.ScrollIntoView(index);
		return WaitForIdleAsync();
	}

	public Task AddStepsAsync(int count)
	{
		for (var i = 0; i < count; i++)
		{
			_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		}

		return WaitForIdleAsync();
	}

	public Task RemoveStepsAsync(int count)
	{
		for (var i = 0; i < count && _surface.StepColumns.Count > 0; i++)
		{
			_fixture.Coordinator.RemoveStep(_surface.StepColumns.Count - 1);
		}

		return WaitForIdleAsync();
	}

	public Task SelectRangeAsync(int from, int to)
	{
		var selectedItems = _stepListBox.SelectedItems
			?? throw new InvalidOperationException("StepListBox exposes no SelectedItems collection.");

		selectedItems.Clear();
		for (var index = from; index <= to; index++)
		{
			selectedItems.Add(_surface.StepColumns[index]);
		}

		return WaitForIdleAsync();
	}

	public Task WaitForIdleAsync()
	{
		Dispatcher.UIThread.RunJobs();
		return Task.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		_window.Close();
		Dispatcher.UIThread.RunJobs();
		await _fixture.DisposeAsync();
	}

	// Resolved from a realized container's visual parent so it stays correct regardless of the ListBox
	// template's chrome.
	private static Visual ResolveItemsPanel(TransposedStepListBox stepListBox)
	{
		var container = stepListBox.GetRealizedContainers()?.FirstOrDefault()
			?? throw new InvalidOperationException(
				"Seed at least one step so the items panel realizes a container for the snapshot scope.");

		return container.GetVisualParent()
			?? throw new InvalidOperationException("Realized container has no visual parent panel.");
	}
}
