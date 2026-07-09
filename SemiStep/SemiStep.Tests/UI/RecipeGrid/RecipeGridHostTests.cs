using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridHostTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Host_WithSurfaceDataContext_RendersCanonicalViewWithRows()
	{
		var host = ShowHost();

		var canonicalView = host.GetVisualDescendants().OfType<CanonicalRecipeGridView>().SingleOrDefault();
		canonicalView.Should().NotBeNull("the host must render the canonical view as its content");

		var dataGrid = FindDataGrid(host);
		var dataGridRows = dataGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
		dataGridRows.Should().HaveCount(SeededStepCount);
	}

	[AvaloniaFact]
	public void Surface_ReturnsHostDataContext()
	{
		var host = ShowHost();

		host.Surface.Should().BeSameAs(_surface);
	}

	[AvaloniaFact]
	public void SelectionRequest_OnSurface_SelectsRowInHostedView()
	{
		var host = ShowHost();

		_surface.RequestSelection(1);
		Dispatcher.UIThread.RunJobs();

		var dataGrid = FindDataGrid(host);
		dataGrid.SelectedIndex.Should().Be(1);
	}

	[AvaloniaFact]
	public void IsEditing_TracksCellEditorLifecycle_ThroughHostForwarding()
	{
		var host = ShowHost();
		var dataGrid = FindDataGrid(host);

		host.IsEditing.Should().BeFalse();

		var durationColumn = dataGrid.Columns.Single(column =>
			column.Tag as string == RecipeTestDriver.StepDurationColumn);
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, durationColumn);
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		host.IsEditing.Should().BeTrue();

		dataGrid.CancelEdit();
		Dispatcher.UIThread.RunJobs();

		host.IsEditing.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ContextMenu_OnWrappingPanel_OpensOnRightClickOverGridRow()
	{
		var contextMenu = new ContextMenu { Items = { new MenuItem { Header = "Add step" } } };
		var host = ShowHost(panel => panel.ContextMenu = contextMenu);
		var dataGrid = FindDataGrid(host);

		var window = _window!;
		var row = dataGrid.GetVisualDescendants().OfType<DataGridRow>().First();
		var center = row.TranslatePoint(
			new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window);
		center.Should().NotBeNull();

		window.MouseDown(center!.Value, MouseButton.Right);
		window.MouseUp(center.Value, MouseButton.Right);
		Dispatcher.UIThread.RunJobs();

		contextMenu.IsOpen.Should().BeTrue(
			"a right-click over grid rows must bubble to the wrapping panel's context menu");
	}

	private RecipeGridHost ShowHost(Action<Panel>? configurePanel = null)
	{
		var host = new RecipeGridHost { DataContext = _surface };
		var panel = new Panel { Children = { host } };
		configurePanel?.Invoke(panel);

		_window = new Window
		{
			Width = 1200,
			Height = 600,
			Content = panel,
		};

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return host;
	}

	private static DataGrid FindDataGrid(RecipeGridHost host)
	{
		var dataGrid = host.GetVisualDescendants().OfType<DataGrid>().SingleOrDefault();
		dataGrid.Should().NotBeNull();

		return dataGrid!;
	}
}
