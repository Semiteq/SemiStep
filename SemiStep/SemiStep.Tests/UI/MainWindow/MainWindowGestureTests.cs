using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using ReactiveUI;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Reactive;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class MainWindowGestureTests : IAsyncLifetime
{
	private const RawInputModifiers Ctrl = RawInputModifiers.Control;
	private const RawInputModifiers CtrlShift = RawInputModifiers.Control | RawInputModifiers.Shift;

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView? _window;
	private int _openDialogs;
	private int _saveDialogs;

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
	public void CtrlN_AddsStep()
	{
		var window = ShowWithGridFocus(stepCount: 1);

		window.KeyPressQwerty(PhysicalKey.N, Ctrl);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void CtrlShiftN_CreatesNewRecipe()
	{
		var window = ShowWithGridFocus(stepCount: 2);

		window.KeyPressQwerty(PhysicalKey.N, CtrlShift);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(0, "a new recipe starts empty");
	}

	[AvaloniaFact]
	public void CtrlO_RunsOpen()
	{
		var window = ShowWithGridFocus(stepCount: 1);

		window.KeyPressQwerty(PhysicalKey.O, Ctrl);
		Dispatcher.UIThread.RunJobs();

		_openDialogs.Should().Be(1);
	}

	[AvaloniaFact]
	public void CtrlS_RunsSave_AndCtrlShiftS_RunsSaveAs()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		var saves = 0;
		var savesAs = 0;
		using var saveCount = _viewModel.RecipeFile.SaveRecipeCommand.Subscribe(_ => saves++);
		using var saveAsCount = _viewModel.RecipeFile.SaveAsRecipeCommand.Subscribe(_ => savesAs++);

		window.KeyPressQwerty(PhysicalKey.S, Ctrl);
		Dispatcher.UIThread.RunJobs();
		window.KeyPressQwerty(PhysicalKey.S, CtrlShift);
		Dispatcher.UIThread.RunJobs();

		saves.Should().Be(1);
		savesAs.Should().Be(1, "Ctrl+Shift+S runs save as");
	}

	[AvaloniaTheory]
	[InlineData(PhysicalKey.Y, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift)]
	public void CtrlZ_Undoes_AndBothRedoGesturesRedo(PhysicalKey redoKey, RawInputModifiers redoModifiers)
	{
		var window = ShowWithGridFocus(stepCount: 2);

		window.KeyPressQwerty(PhysicalKey.Z, Ctrl);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(1, "Ctrl+Z undoes the last append");

		window.KeyPressQwerty(redoKey, redoModifiers);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void Delete_RemovesSelectedStep_AndCtrlZRestoresIt()
	{
		var window = ShowWithGridFocus(stepCount: 2);

		window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(1);

		window.KeyPressQwerty(PhysicalKey.Z, Ctrl);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(2);
	}

	[AvaloniaTheory]
	[InlineData(PhysicalKey.N, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.N, RawInputModifiers.Control | RawInputModifiers.Shift)]
	[InlineData(PhysicalKey.O, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.S, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift)]
	[InlineData(PhysicalKey.Z, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.Y, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift)]
	[InlineData(PhysicalKey.Delete, RawInputModifiers.None)]
	[InlineData(PhysicalKey.C, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.X, RawInputModifiers.Control)]
	[InlineData(PhysicalKey.V, RawInputModifiers.Control)]
	public void Gesture_FromAFocusedTextBox_RunsNoCommand(PhysicalKey key, RawInputModifiers modifiers)
	{
		var window = ShowWithGridFocus(stepCount: 3);
		_viewModel.RecipeCommands.UndoCommand.ExecuteIfPossible();
		using var executions = CountCommandExecutions();
		var stepsBefore = StepCount;
		OpenTextEditor(window);

		window.KeyPressQwerty(key, modifiers);
		Dispatcher.UIThread.RunJobs();

		executions.Count.Should().Be(0, "a key typed into a text box runs no window command");
		StepCount.Should().Be(stepsBefore);
		_openDialogs.Should().Be(0);
		_saveDialogs.Should().Be(0);
	}

	[AvaloniaTheory]
	[InlineData(PhysicalKey.C)]
	[InlineData(PhysicalKey.X)]
	[InlineData(PhysicalKey.V)]
	public void CopyCutPaste_FromGridFocus_RunTheirCommandOnce(PhysicalKey key)
	{
		var window = ShowWithGridFocus(stepCount: 2);
		IObservable<Unit> expected = key switch
		{
			PhysicalKey.C => _viewModel.Clipboard.CopyStepCommand,
			PhysicalKey.X => _viewModel.Clipboard.CutStepCommand,
			_ => _viewModel.Clipboard.PasteStepCommand
		};
		using var executions = CountCommandExecutions();
		using var expectedExecutions = new ExecutionCounter(expected);

		window.KeyPressQwerty(key, Ctrl);
		Dispatcher.UIThread.RunJobs();

		expectedExecutions.Count.Should().Be(1);
		executions.Count.Should().Be(1, "the gesture runs no other command");
	}

	[AvaloniaFact]
	public void Delete_WhileTheRecipeIsLocked_RunsNothing()
	{
		var window = ShowWithGridFocus(stepCount: 2);
		_fixture.SetRecipeActive(true);
		Dispatcher.UIThread.RunJobs();
		((ICommand)_viewModel.RecipeCommands.DeleteStepCommand).CanExecute(null)
			.Should().BeFalse("precondition: a running recipe locks editing");
		using var executions = CountCommandExecutions();

		window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		executions.Count.Should().Be(0);
		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void Gesture_HandledByAControlBeforeTheWindow_IsNeverDispatchedToTheWindow()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		dataGrid.AddHandler(InputElement.KeyDownEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble);
		using var executions = CountCommandExecutions();

		window.KeyPressQwerty(PhysicalKey.N, Ctrl);
		Dispatcher.UIThread.RunJobs();

		executions.Count.Should().Be(0, "a key a control took is not a window shortcut");
		StepCount.Should().Be(1);
	}

	[AvaloniaFact]
	public void CtrlN_WithTheToolBarHidden_AddsStep()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		_viewModel.ToggleToolBarCommand.ExecuteIfPossible();
		Dispatcher.UIThread.RunJobs();
		window.GetVisualDescendants().OfType<RecipeToolBar>().Should().NotContain(
			toolBar => toolBar.IsEffectivelyVisible,
			"precondition: the toolbar is hidden");

		window.KeyPressQwerty(PhysicalKey.N, Ctrl);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void Delete_WithOpenCanonicalComboList_RemovesNoStep()
	{
		var window = ShowWithGridFocus(stepCount: 2);
		var combo = FocusCanonicalActionCombo(window);
		combo.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();

		window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(2, "an open combo list counts as editing");
	}

	[AvaloniaFact]
	public void Delete_WithClosedCanonicalComboFocused_RemovesTheStep()
	{
		var window = ShowWithGridFocus(stepCount: 2);
		FocusCanonicalActionCombo(window);

		window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(1, "a closed combo is not editing");
	}

	[AvaloniaFact]
	public void CtrlZ_AndCtrlN_FromAFocusedCanonicalCombo_Fire()
	{
		var window = ShowWithGridFocus(stepCount: 2);
		FocusCanonicalActionCombo(window);

		window.KeyPressQwerty(PhysicalKey.Z, Ctrl);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(1);

		window.KeyPressQwerty(PhysicalKey.N, Ctrl);
		Dispatcher.UIThread.RunJobs();
		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void Delete_WithOpenTransposedCombo_RemovesNoStep()
	{
		var window = ShowWithGridFocus(stepCount: 2);
		_viewModel.ToggleOrientationCommand.ExecuteIfPossible();
		Dispatcher.UIThread.RunJobs();
		_viewModel.RecipeGrid.UpdateSelection([0]);

		var presenter = window.GetVisualDescendants()
			.OfType<TransposedComboCellPresenter>()
			.First(candidate => candidate.DataContext is ParameterCellViewModel { Descriptor.ParameterKey: "action" });
		presenter.Focus();
		window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();
		presenter.IsEditing.Should().BeTrue("precondition: F2 opened the transposed combo");

		window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		StepCount.Should().Be(2);
	}

	[AvaloniaFact]
	public void F2_OnTheCurrentCanonicalCell_StartsEditing()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, DurationColumn(dataGrid));

		window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		window.GetVisualDescendants().OfType<RecipeGridHost>().Single().IsEditing.Should().BeTrue();
	}

	[AvaloniaFact]
	public void AltF4_RunsNothingThroughTheGestureHandler()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		var exits = 0;
		using var exitCount = _viewModel.ExitCommand.Subscribe(_ => exits++);

		window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.Alt);
		Dispatcher.UIThread.RunJobs();

		exits.Should().Be(0, "the window system owns Alt+F4");
		window.IsVisible.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Gesture_WhoseCommandThrows_IsReportedOnce()
	{
		var window = ShowWithGridFocus(stepCount: 1);
		var grid = (ActiveRecipeGridSurface)_viewModel.RecipeGrid;
		using var poison = grid.WhenAnyValue(x => x.Orientation)
			.Skip(1)
			.Subscribe(_ => throw new InvalidOperationException("boom"));

		var press = () =>
		{
			window.KeyPressQwerty(PhysicalKey.T, CtrlShift);
			Dispatcher.UIThread.RunJobs();
		};

		press.Should().NotThrow();
		_fixture.MessagePanel.Entries.Should().ContainSingle(entry =>
			entry.Severity == MessageSeverity.Error && entry.Message == $"{Resources.OrientationToggleFailed}: boom");
	}

	private int StepCount => _fixture.Coordinator.CurrentRecipe.StepCount;

	private MainWindowView ShowWithGridFocus(int stepCount)
	{
		_fixture.SeedRecipe(stepCount);
		_viewModel.RecipeGrid.Initialize();

		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		_viewModel.RecipeFile.OpenFileInteraction.RegisterHandler(context =>
		{
			_openDialogs++;
			context.SetOutput(null);
		});
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context =>
		{
			_saveDialogs++;
			context.SetOutput(null);
		});

		var dataGrid = _window.GetVisualDescendants().OfType<DataGrid>().Single();
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, DurationColumn(dataGrid));

		return _window;
	}

	private static DataGridColumn DurationColumn(DataGrid dataGrid)
	{
		return dataGrid.Columns.Single(column => (column.Tag as string) == RecipeTestDriver.StepDurationColumn);
	}

	private static void OpenTextEditor(MainWindowView window)
	{
		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		DataGridTestHelper.SetCurrentCell(dataGrid, rowIndex: 0, DurationColumn(dataGrid));
		dataGrid.BeginEdit();
		Dispatcher.UIThread.RunJobs();

		dataGrid.GetVisualDescendants().OfType<TextBox>().Single(textBox => textBox.IsFocused)
			.Should().NotBeNull("precondition: a cell text editor has focus");
	}

	private static ComboBox FocusCanonicalActionCombo(MainWindowView window)
	{
		var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().Single();
		var combo = dataGrid.GetVisualDescendants().OfType<ComboBox>().First(candidate => candidate.IsEffectivelyEnabled);
		combo.Focus();
		Dispatcher.UIThread.RunJobs();
		combo.IsFocused.Should().BeTrue("precondition: the combo cell has focus");

		return combo;
	}

	private ExecutionCounter CountCommandExecutions()
	{
		return new ExecutionCounter(
			_viewModel.RecipeCommands.AddStepCommand,
			_viewModel.RecipeCommands.DeleteStepCommand,
			_viewModel.RecipeCommands.UndoCommand,
			_viewModel.RecipeCommands.RedoCommand,
			_viewModel.RecipeFile.NewRecipeCommand,
			_viewModel.RecipeFile.LoadRecipeCommand,
			_viewModel.RecipeFile.SaveRecipeCommand.Select(_ => Unit.Default),
			_viewModel.RecipeFile.SaveAsRecipeCommand.Select(_ => Unit.Default),
			_viewModel.Clipboard.CopyStepCommand,
			_viewModel.Clipboard.CutStepCommand,
			_viewModel.Clipboard.PasteStepCommand);
	}

	private sealed class ExecutionCounter : IDisposable
	{
		private readonly CompositeDisposable _subscriptions = [];

		public ExecutionCounter(params IObservable<Unit>[] commands)
		{
			foreach (var command in commands)
			{
				_subscriptions.Add(command.Subscribe(_ => Count++));
			}
		}

		public int Count { get; private set; }

		public void Dispose()
		{
			_subscriptions.Dispose();
		}
	}
}
