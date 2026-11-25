using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "StatusBar")]
[Trait("Category", "Unit")]
public sealed class AppStatusBarTimerFontTests : IAsyncLifetime
{
	private const int BarFontSize = 16;

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData("StepTimerSlot")]
	[InlineData("RecipeTimerSlot")]
	public void TimerText_UsesTheBarFontSize(string slotName)
	{
		var defaults = GridStyleOptions.Default;
		var statusBar = ShowStatusBar(defaults with
		{
			StatusBar = defaults.StatusBar with { FontSize = BarFontSize }
		});

		var slot = statusBar.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == slotName);
		var texts = slot.GetVisualDescendants().OfType<TextBlock>().ToList();

		texts.Should().NotBeEmpty();
		texts.Should().AllSatisfy(text => text.FontSize.Should().Be(BarFontSize));
	}

	private AppStatusBar ShowStatusBar(GridStyleOptions gridStyle)
	{
		var statusBar = new AppStatusBar { DataContext = _viewModel };
		_window = new Window { Content = statusBar };
		CellPaletteInstaller.Install(_window.Resources, gridStyle);
		_window.Show();

		return statusBar;
	}
}
