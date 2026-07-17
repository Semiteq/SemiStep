using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

namespace SemiStep.Tests.Performance.Harness;

// Drives the real canonical DataGrid view over its public surface: ScrollIntoView on the DataGrid, its
// SelectedItems, and the coordinator mutation commands. Symmetric with TransposedGridDriver so one
// parity scenario body runs against both.
public sealed class CanonicalGridDriver : IRecipeGridDriver
{
	private const int DefaultWindowWidth = 1200;
	private const int DefaultWindowHeight = 400;

	private readonly UIFixture _fixture;
	private readonly CanonicalRecipeGridSurface _surface;
	private readonly DataGrid _dataGrid;
	private readonly Window _window;
	private readonly Visual _snapshotScope;

	private CanonicalGridDriver(
		UIFixture fixture,
		CanonicalRecipeGridSurface surface,
		DataGrid dataGrid,
		Window window,
		Visual snapshotScope)
	{
		_fixture = fixture;
		_surface = surface;
		_dataGrid = dataGrid;
		_window = window;
		_snapshotScope = snapshotScope;
	}

	public TopLevel Root => _window;

	public Visual SnapshotScope => _snapshotScope;

	public int ItemCount => _surface.RecipeRows.Count;

	public IReadOnlyList<int> SelectedIndices => _surface.SelectedStepIndices;

	public IReadOnlyList<int> RealizedIndices => _dataGrid
				.GetVisualDescendants()
				.OfType<DataGridRow>()
				.Select(row => row.DataContext)
				.OfType<RecipeRowViewModel>()
				.Select(row => _surface.RecipeRows.IndexOf(row))
				.Where(index => index >= 0)
				.OrderBy(index => index)
				.ToList();

	public IReadOnlyList<Control> RealizedContainers => _dataGrid
				.GetVisualDescendants()
				.OfType<DataGridRow>()
				.Cast<Control>()
				.ToList();

	public static async Task<CanonicalGridDriver> CreateAsync(
		string configName = "WithGroups",
		int stepCount = 60,
		int windowWidth = DefaultWindowWidth,
		int windowHeight = DefaultWindowHeight)
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(configName);
		fixture.SeedRecipe(stepCount);

		var surface = fixture.CreateCanonicalSurface();
		surface.Initialize();

		var view = new CanonicalRecipeGridView { DataContext = surface };
		var window = new Window { Width = windowWidth, Height = windowHeight, Content = view };
		window.Show();
		Dispatcher.UIThread.RunJobs();

		var dataGrid = view.FindControl<DataGrid>("RecipeGrid")
			?? throw new InvalidOperationException(
				"CanonicalRecipeGridView is missing its RecipeGrid anchor.");

		var snapshotScope = ResolveRowsPresenter(dataGrid);

		return new CanonicalGridDriver(fixture, surface, dataGrid, window, snapshotScope);
	}

	public Task ScrollToColumnAsync(int index)
	{
		_dataGrid.ScrollIntoView(_surface.RecipeRows[index], null);
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
		for (var i = 0; i < count && _surface.RecipeRows.Count > 0; i++)
		{
			_fixture.Coordinator.RemoveStep(_surface.RecipeRows.Count - 1);
		}

		return WaitForIdleAsync();
	}

	public Task SelectRangeAsync(int from, int to)
	{
		_dataGrid.SelectedItems.Clear();
		for (var index = from; index <= to; index++)
		{
			_dataGrid.SelectedItems.Add(_surface.RecipeRows[index]);
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

	// The snapshot scope must be the rows-presenter subtree, not the window: resolve it from a realized
	// row's visual parent so it stays correct regardless of the DataGrid template's chrome.
	private static Visual ResolveRowsPresenter(DataGrid dataGrid)
	{
		var row = dataGrid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault()
			?? throw new InvalidOperationException(
				"Seed at least one step so the DataGrid realizes a row for the snapshot scope.");

		return row.GetVisualParent()
			?? throw new InvalidOperationException("Realized row has no visual parent presenter.");
	}
}
