using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeGrid;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class ToggleOrientationCommandTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView? _window;

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
	public void ToggleOrientationCommand_FlipsOrientationBothWays()
	{
		_viewModel.IsTransposedOrientation.Should().BeFalse("canonical is the startup default");

		_viewModel.ToggleOrientationCommand.Execute().Subscribe();
		_viewModel.IsTransposedOrientation.Should().BeTrue();

		_viewModel.ToggleOrientationCommand.Execute().Subscribe();
		_viewModel.IsTransposedOrientation.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CtrlShiftT_FlipsOrientation()
	{
		var window = ShowMainWindow();

		window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.Control | RawInputModifiers.Shift);
		Dispatcher.UIThread.RunJobs();

		_viewModel.IsTransposedOrientation.Should().BeTrue();
	}

	[AvaloniaFact]
	public void CtrlShiftT_Twice_ReturnsToCanonical()
	{
		var window = ShowMainWindow();

		var modifiers = RawInputModifiers.Control | RawInputModifiers.Shift;
		window.KeyPressQwerty(PhysicalKey.T, modifiers);
		window.KeyPressQwerty(PhysicalKey.T, modifiers);
		Dispatcher.UIThread.RunJobs();

		_viewModel.IsTransposedOrientation.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CtrlShiftT_FlipsOrientation_WhileCellEditorIsOpen()
	{
		_fixture.SeedRecipe(2);
		_viewModel.RecipeGrid.Initialize();
		var window = ShowMainWindow();

		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		var durationColumn = dataGrid.Columns.Single(column =>
			column.Tag as string == RecipeTestDriver.StepDurationColumn);
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, durationColumn);
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		var host = window.GetVisualDescendants().OfType<RecipeGridHost>().Single();
		host.IsEditing.Should().BeTrue("the precondition is an open cell editor");

		window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.Control | RawInputModifiers.Shift);
		Dispatcher.UIThread.RunJobs();

		_viewModel.IsTransposedOrientation.Should().BeTrue(
			"the orientation hotkey must stay live while an editor is open");
	}

	[AvaloniaFact]
	public void CtrlShiftT_WithUncommittedEditorValue_CommitsValue_AndLeavesNoStaleEditSession()
	{
		_fixture.SeedRecipe(2);
		_viewModel.RecipeGrid.Initialize();
		var window = ShowMainWindow();

		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		var durationColumn = dataGrid.Columns.Single(column =>
			column.Tag as string == RecipeTestDriver.StepDurationColumn);
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, durationColumn);
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		var editor = dataGrid.GetVisualDescendants().OfType<TextBox>().Single(textBox => textBox.IsFocused);
		editor.Text = "45";

		var modifiers = RawInputModifiers.Control | RawInputModifiers.Shift;
		window.KeyPressQwerty(PhysicalKey.T, modifiers);
		Dispatcher.UIThread.RunJobs();

		_viewModel.IsTransposedOrientation.Should().BeTrue();
		_fixture.Coordinator.CurrentRecipe.Steps[0]
			.Properties.Values.Select(property => property.Value)
			.Should().Contain(45f, "the keyed-in value must be committed, not silently dropped, by the flip");

		window.KeyPressQwerty(PhysicalKey.T, modifiers);
		Dispatcher.UIThread.RunJobs();

		var host = window.GetVisualDescendants().OfType<RecipeGridHost>().Single();
		host.IsEditing.Should().BeFalse("no stale editing session may survive the flip round-trip");

		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 1, durationColumn);
		dataGrid.BeginEdit().Should().BeTrue("a fresh edit must start cleanly after the round-trip");
	}

	private MainWindowView ShowMainWindow()
	{
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return _window;
	}
}
