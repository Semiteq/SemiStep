using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class AppStatusBarTimerFontTests : IAsyncLifetime
{
	private const double TimerLabelFontSize = 16;
	private const double TimerValueFontSize = 28;

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

	[AvaloniaFact]
	public void Timer_LabelAndValueTextBlocks_RenderAtTheirSeparateRoleSizes()
	{
		var statusBar = ShowStatusBar(GridStyleOptions.Default with
		{
			StatusBarTimerLabelFontSize = (int)TimerLabelFontSize,
			StatusBarTimerLabelFontWeight = 400,
			StatusBarTimerValueFontSize = (int)TimerValueFontSize,
			StatusBarTimerValueFontWeight = 700
		});

		var labels = statusBar.GetVisualDescendants()
			.OfType<TextBlock>()
			.Where(textBlock => textBlock.Text is "Шаг:" or "Рецепт:")
			.ToList();

		labels.Should().HaveCount(2, "each timer row carries a Шаг:/Рецепт: label TextBlock");

		foreach (var label in labels)
		{
			label.FontSize.Should().Be(TimerLabelFontSize,
				"the timer label TextBlock must bind the label-role size");

			var row = (StackPanel)label.Parent!;
			var value = row.Children
				.OfType<TextBlock>()
				.Single(textBlock => !ReferenceEquals(textBlock, label));

			value.FontSize.Should().Be(TimerValueFontSize,
				"the paired timer value TextBlock must bind the value-role size, not the label-role size");
		}
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
