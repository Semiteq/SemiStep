using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tools.PlcEmulator.Ui;

using Xunit;

using static SemiStep.Tests.Emulator.RunEngineHarness;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "UI")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class EmulatorWindowViewTests
{
	[AvaloniaFact]
	public async Task View_BindsTheStateTheTableAndTheButtons()
	{
		using var harness = await EmulatorWindowHarness.CreateAsync();
		var viewModel = harness.ViewModel;
		var window = new MainWindow { DataContext = viewModel };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var grid = window.FindControl<DataGrid>("MemoryGrid")!;
			var subtitle = window.FindControl<TextBlock>("SubtitleTextBlock")!;

			window.Title.Should().Be(viewModel.Title);
			grid.IsReadOnly.Should().BeFalse();
			grid.ItemsSource.Should().BeSameAs(viewModel.SelectedBlock.Rows);
			grid.Columns.Select(column => column.Header).Should().Equal("Offset", "Name", "Type", "Value");
			window.FindControl<Button>("StartButton")!.IsEffectivelyEnabled.Should().BeTrue();
			window.FindControl<Button>("StopButton")!.IsEffectivelyEnabled.Should().BeFalse();
			var idleSubtitle = subtitle.Text;

			harness.Engine.Commit(RecipeOf(Wait(1f)));
			await viewModel.Start.Execute();
			Dispatcher.UIThread.RunJobs();

			grid.IsReadOnly.Should().BeTrue();
			subtitle.Text.Should().NotBe(idleSubtitle);
			window.FindControl<TextBlock>("StateTextBlock")!.Text.Should().Be(viewModel.State.Text);
			window.FindControl<Button>("StartButton")!.IsEffectivelyEnabled.Should().BeFalse();
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public async Task View_CommittingTheValueEditor_WritesThroughTheViewModel()
	{
		using var harness = await EmulatorWindowHarness.CreateAsync();
		var intDb = harness.Plc.Layout.IntDb;
		harness.ViewModel.SelectedBlock = harness.Block(intDb.DbNumber);
		var window = new MainWindow { DataContext = harness.ViewModel };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var grid = window.FindControl<DataGrid>("MemoryGrid")!;
			var row = harness.Row("current_size");

			BeginValueEdit(grid, row);
			row.IsEditing.Should().BeTrue();
			grid.GetVisualDescendants().OfType<TextBox>().Single().Text = "4";
			grid.CommitEdit();
			Dispatcher.UIThread.RunJobs();

			row.IsEditing.Should().BeFalse();
			harness.ReadInt(intDb.DbNumber, intDb.CurrentSizeOffset).Should().Be(4);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public async Task View_SwitchingTheBlockMidEdit_WritesTheEditToTheEditedBlock()
	{
		using var harness = await EmulatorWindowHarness.CreateAsync();
		var intDb = harness.Plc.Layout.IntDb;
		var floatDb = harness.Plc.Layout.FloatDb;
		harness.ViewModel.SelectedBlock = harness.Block(intDb.DbNumber);
		var window = new MainWindow { DataContext = harness.ViewModel };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var grid = window.FindControl<DataGrid>("MemoryGrid")!;
			BeginValueEdit(grid, harness.Row("current_size"));
			grid.GetVisualDescendants().OfType<TextBox>().Single().Text = "4";

			harness.ViewModel.SelectedBlock = harness.Block(floatDb.DbNumber);
			Dispatcher.UIThread.RunJobs();

			harness.ReadInt(intDb.DbNumber, intDb.CurrentSizeOffset).Should().Be(4);
			harness.ReadInt(floatDb.DbNumber, floatDb.CurrentSizeOffset).Should().Be(0);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public async Task View_CtrlClickOnTheSelectedBlock_KeepsABlockSelected()
	{
		using var harness = await EmulatorWindowHarness.CreateAsync();
		var selected = harness.ViewModel.SelectedBlock;
		var window = new MainWindow { DataContext = harness.ViewModel };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var list = window.GetVisualDescendants().OfType<ListBox>().Single();
			var point = list.ContainerFromItem(selected)!.TranslatePoint(new Point(20, 10), window)!.Value;

			window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
			window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
			Dispatcher.UIThread.RunJobs();
			harness.Refresh();

			harness.ViewModel.SelectedBlock.Should().NotBeNull();
			list.SelectedItem.Should().BeSameAs(harness.ViewModel.SelectedBlock);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public async Task View_CurrentCell_DrawsNoFocusFrame()
	{
		using var harness = await EmulatorWindowHarness.CreateAsync();
		var window = new MainWindow { DataContext = harness.ViewModel };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var grid = window.FindControl<DataGrid>("MemoryGrid")!;
			grid.SelectedItem = harness.Row("actual_line");
			grid.CurrentColumn = grid.Columns[3];
			grid.Focus();
			Dispatcher.UIThread.RunJobs();

			var frames = grid.GetVisualDescendants()
				.OfType<Rectangle>()
				.Where(IsCellFrame)
				.ToList();

			frames.Should().NotBeEmpty();
			frames.Select(rectangle => (rectangle.Stroke as ISolidColorBrush)?.Color.A).Should().AllBeEquivalentTo(0);
		}
		finally
		{
			window.Close();
		}
	}

	private static void BeginValueEdit(DataGrid grid, MemoryRowViewModel row)
	{
		grid.SelectedItem = row;
		grid.CurrentColumn = grid.Columns[3];
		grid.BeginEdit();
		Dispatcher.UIThread.RunJobs();
	}

	private static bool IsCellFrame(Rectangle rectangle)
	{
		return rectangle.Name == "CurrencyVisual" || rectangle.GetVisualParent() is Panel { Name: "FocusVisual" };
	}
}
