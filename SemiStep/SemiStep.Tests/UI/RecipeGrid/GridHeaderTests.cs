using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;
using SemiStep.UI.Localization;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "GridHeader")]
[Trait("Category", "Integration")]
public sealed class GridHeaderTests : IAsyncLifetime
{
	private static readonly Color _headerGrey = Color.Parse("#6C707E");

	private readonly UIFixture _fixture = new();
	private CanonicalGridWindow? _grid;
	private Window? _window;

	// The shipped MBE preset carries the long Russian headers that trim at their default width.
	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync(ShippedConfigLocator.GetConfigDirectory("MBE"));
	}

	public async ValueTask DisposeAsync()
	{
		_grid?.Dispose();
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void RenderedHeader_Is12SemiBoldGrey_OnOneLine()
	{
		ShowCanonicalGrid();

		foreach (var text in HeaderTexts(_grid!.DataGrid))
		{
			text.FontSize.Should().Be(12, $"header '{text.Text}' is 12 px");
			text.FontWeight.Should().Be(FontWeight.SemiBold, $"header '{text.Text}' is SemiBold");
			((ISolidColorBrush)text.Foreground!).Color.Should().Be(_headerGrey, $"header '{text.Text}' is grey");
			text.MaxLines.Should().Be(1, $"header '{text.Text}' stays on one line");
			text.TextTrimming.Should().Be(TextTrimming.CharacterEllipsis);
		}
	}

	[AvaloniaFact]
	public void EveryHeader_CarriesItsFullTextAsTooltip_IncludingATrimmedOne()
	{
		ShowCanonicalGrid();
		var texts = HeaderTexts(_grid!.DataGrid);

		foreach (var text in texts)
		{
			ToolTip.GetTip(text).Should().Be(text.Text, $"header '{text.Text}' shows its full text as a tooltip");
		}

		texts.Should().Contain(
			text => text.TextLayout.TextLines.Any(line => line.HasCollapsed),
			"at least one real column trims its header at its default width");
	}

	[AvaloniaFact]
	public void NumberHeader_AndTransposedCorner_ReadTheResource()
	{
		ShowCanonicalGrid();

		_grid!.DataGrid.Columns[0].Header.Should().Be(Resources.StepNumberHeader);

		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 800, Height = 400, Content = view };
		view.FindControl<TransposedStepListBox>("StepListBox")!.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		view.GetVisualDescendants().OfType<TextBlock>()
			.Should().Contain(text => text.Text == Resources.StepNumberHeader, "the transposed corner reads the resource");
	}

	[AvaloniaFact]
	public void TransposedCorner_Is12SemiBoldGrey_LikeTheCanonicalNumberHeader()
	{
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		_window = new Window { Width = 800, Height = 400, Content = view };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		view.FindControl<TransposedStepListBox>("StepListBox")!.UseTransposedColumnsPanel();
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		var corner = view.GetVisualDescendants().OfType<TextBlock>()
			.Single(text => text.Text == Resources.StepNumberHeader);

		corner.FontSize.Should().Be(12);
		corner.FontWeight.Should().Be(FontWeight.SemiBold);
		((ISolidColorBrush)corner.Foreground!).Color.Should().Be(_headerGrey);
	}

	// Production installs the config palette at startup (App.axaml.cs); the header brush comes from it.
	private void ShowCanonicalGrid()
	{
		_grid = new CanonicalGridWindow(_fixture);
		CellPaletteInstaller.Install(_grid.Window.Resources, _fixture.AppConfiguration.GridStyle);
		Dispatcher.UIThread.RunJobs();
	}

	private static List<TextBlock> HeaderTexts(DataGrid dataGrid)
	{
		return [.. dataGrid.GetVisualDescendants()
			.OfType<DataGridColumnHeader>()
			.Where(header => header.Content is string)
			.Select(header => header.GetVisualDescendants().OfType<TextBlock>()
				.First(text => text.Text == (string)header.Content!))];
	}
}
