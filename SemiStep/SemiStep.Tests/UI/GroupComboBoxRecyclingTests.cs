using System.Globalization;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

/// <summary>
/// Regression tests for the group ComboBox cell template. Covers two hazards: ItemsSource
/// staleness on cell recycle (DataContext swap must propagate to the bound per-row group items
/// list), and writeback (SelectionChanged in the cell template forwards the chosen item's Id
/// to the row VM, bypassing the binding pipeline for the write direction).
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class GroupComboBoxRecyclingTests : IAsyncLifetime
{
	private const int RecycledStepCount = 60;

	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void GroupComboBox_ItemsSource_ReflectsActionGroupName()
	{
		var template = GetGroupColumnCellTemplate();
		var rowWithGroup = CreateRow(RecipeTestDriver.WithGroupActionId);

		var (_, comboBox) = MaterializeComboBox(template, rowWithGroup);

		var items = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>(
				"the group column's ItemsSource must bind to the row VM's per-column group items list").Subject;

		items.Should().NotBeEmpty(
			"action 50 (Valve) defines a target column bound to the 'valve' group which has items");

		items.Select(item => item.DisplayText).Should().Contain(["Open", "Close"]);
	}

	[AvaloniaFact]
	public void GroupComboBox_ItemsSource_RefreshesWhenDataContextSwapsToRowWithGroup()
	{
		// This is the regression case for the recycling bug. Materialise a ComboBox on a row
		// whose action has no group-bound column (Wait). Then swap the DataContext onto a row
		// whose action does have a group column. ItemsSource must update.
		var template = GetGroupColumnCellTemplate();
		var rowWithoutGroup = CreateRow(RecipeTestDriver.WaitActionId);
		var rowWithGroup = CreateRow(RecipeTestDriver.WithGroupActionId);

		var (presenter, comboBox) = MaterializeComboBox(template, rowWithoutGroup);

		comboBox.ItemsSource.Should().NotBeNull(
			"target column is pre-populated with an empty list on every row so binding never sees UnsetValue");
		var initialItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IEnumerable<ComboBoxItemViewModel>>().Subject;
		initialItems.Should().BeEmpty(
			"Wait action does not declare a 'target' property, so the pre-populated list is empty");

		RecycleOnto(presenter, rowWithGroup);

		var refreshedItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>(
				"DataContext swap must propagate through the ItemsSource binding to the new row's group items").Subject;

		refreshedItems.Should().NotBeEmpty(
			"after swapping DataContext to a Valve-action row, the ComboBox must surface the valve group items");

		refreshedItems.Select(item => item.DisplayText).Should().Contain(["Open", "Close"]);
	}

	[AvaloniaFact]
	public void GroupComboBox_ItemsSource_BecomesEmpty_WhenDataContextSwapsAwayFromGroupAction()
	{
		// Inverse of the above. Start with a row whose action has a group column, then recycle
		// onto a row whose action lacks one. ItemsSource must shrink to empty so the dropdown
		// does not display the previous row's items.
		var template = GetGroupColumnCellTemplate();
		var rowWithGroup = CreateRow(RecipeTestDriver.WithGroupActionId);
		var rowWithoutGroup = CreateRow(RecipeTestDriver.WaitActionId);

		var (presenter, comboBox) = MaterializeComboBox(template, rowWithGroup);

		comboBox.ItemsSource.Should().NotBeNull();

		RecycleOnto(presenter, rowWithoutGroup);

		comboBox.ItemsSource.Should().NotBeNull(
			"target column stays pre-populated; the list shrinks to empty rather than the binding going null");
		var swappedItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IEnumerable<ComboBoxItemViewModel>>().Subject;
		swappedItems.Should().BeEmpty(
			"after swapping onto a row without a group-bound column, no items must leak from the previous row");
	}

	[AvaloniaFact]
	public void GroupComboBox_ItemsSource_RefreshesWhenSwappingBetweenTwoDifferentGroupActions()
	{
		// Real-world MBE scenario: a single `target` column resolves to different groups based on
		// the action (Valve → 'valve', Heater → 'heater', etc.). If ItemsSource latched to the
		// first group it ever saw, subsequent group-bearing rows would still display Valve items.
		var template = GetGroupColumnCellTemplate();

		var firstAction = new ActionDefinition(
			id: 9001,
			uiName: "FirstGroupAction",
			deployDuration: DeployDuration.Immediate,
			properties:
			[
				new ActionPropertyDefinition(
					Key: RecipeTestDriver.TargetColumn,
					GroupName: "first_group",
					PropertyTypeId: "enum",
					DefaultValue: null),
			]);
		var secondAction = new ActionDefinition(
			id: 9002,
			uiName: "SecondGroupAction",
			deployDuration: DeployDuration.Immediate,
			properties:
			[
				new ActionPropertyDefinition(
					Key: RecipeTestDriver.TargetColumn,
					GroupName: "second_group",
					PropertyTypeId: "enum",
					DefaultValue: null),
			]);

		var registry = BuildTwoGroupRegistry(firstAction, secondAction);
		var comboBoxItemsProvider = new ComboBoxItemsProvider(registry);
		var firstRow = new RecipeRowViewModel(
			1,
			new Step(9001, []),
			firstAction,
			registry,
			comboBoxItemsProvider,
			new HashSet<string>());
		var secondRow = new RecipeRowViewModel(
			2,
			new Step(9002, []),
			secondAction,
			registry,
			comboBoxItemsProvider,
			new HashSet<string>());

		// Re-resolve the template against the synthetic registry. The CellTemplate is wired via
		// `Binding(GroupItemsByColumn[target])` which queries the row VM, so the registry used
		// by ColumnBuilder for template construction does not need to know about these synthetic
		// groups — but the registry consulted by RecipeRowViewModel does. Materialising directly
		// against the existing template with the synthetic row VMs exercises that exact path.
		var (presenter, comboBox) = MaterializeComboBox(template, firstRow);

		var firstItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		firstItems.Select(item => item.DisplayText).Should().BeEquivalentTo("alpha", "beta");

		RecycleOnto(presenter, secondRow);

		var secondItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		secondItems.Select(item => item.DisplayText).Should().BeEquivalentTo("gamma", "delta");
	}

	private static RecipeMetadataRegistry BuildTwoGroupRegistry(params ActionDefinition[] actions)
	{
		var groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["first_group"] = new GroupDefinition(
				GroupId: "first_group",
				Items: new Dictionary<int, string> { [1] = "alpha", [2] = "beta" }),
			["second_group"] = new GroupDefinition(
				GroupId: "second_group",
				Items: new Dictionary<int, string> { [1] = "gamma", [2] = "delta" }),
		};

		var columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			[RecipeTestDriver.TargetColumn] = new GridColumnDefinition(
				Key: RecipeTestDriver.TargetColumn,
				ColumnType: ColumnTypes.ActionTargetComboBox,
				UiName: "Target",
				PropertyTypeId: "enum",
				ReadOnly: false,
				SaveToCsv: false),
		};

		var properties = TestRecipeMetadataRegistryFactory.DefaultStringProperty();
		properties["enum"] = TestPropertyTypeDefinitionBuilder.CreateInt("enum");

		var config = new AppConfiguration(
			Properties: properties,
			Columns: columns,
			Groups: groups,
			Actions: actions.ToDictionary(action => action.Id),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: new AppUiOptions("ru"));

		return new RecipeMetadataRegistry(config);
	}

	[AvaloniaFact]
	public void GroupComboBox_UserSelection_PropagatesToRowVM()
	{
		// Writeback regression guard. Avalonia 12.0.3's MultiBinding ConvertBack is dead code and
		// TwoWay SelectedValue has open spurious-write bugs, so writeback flows through a
		// SelectionChanged handler in the cell template. This test exercises the end-to-end path:
		// assigning a value to ComboBox.SelectedItem must surface as a PropertyValueChanged event
		// on the row VM with the chosen item's Id stringified.
		var template = GetGroupColumnCellTemplate();
		var row = CreateRow(RecipeTestDriver.WithGroupActionId);

		var (_, comboBox) = MaterializeComboBox(template, row);

		var items = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		items.Should().NotBeEmpty();

		var captured = new List<(string Key, string? Value)>();
		row.PropertyValueChanged += (key, value) => captured.Add((key, value));

		var chosen = items[0];
		comboBox.SelectedItem = chosen;

		captured.Should().ContainSingle(
			"the SelectionChanged handler must forward the chosen item's Id once to the row VM");
		captured[0].Key.Should().Be(RecipeTestDriver.TargetColumn);
		captured[0].Value.Should().Be(chosen.Id.ToString(CultureInfo.InvariantCulture));
	}

	[AvaloniaFact]
	public void GroupComboBox_DataContextSwap_DoesNotPropagatePhantomWritesToEitherRow()
	{
		// Recycling regression guard. When the cell is recycled onto a new row, ItemsSource and
		// SelectedValue both re-resolve and ComboBox raises SelectionChanged as a side effect.
		// Neither the previous row (no longer being edited) nor the new row (whose value should
		// already match its model) must observe a PropertyValueChanged write from this transition.
		// Defenses in play: the SelectionChanged handler returns early on non-ComboBoxItemViewModel
		// selections (covers the swap-to-empty-items case), and SetPropertyValue's equal-value
		// guard absorbs same-value writes (covers the swap-between-matching-groups case).
		var template = GetGroupColumnCellTemplate();
		var rowWithGroup = CreateRow(RecipeTestDriver.WithGroupActionId);
		var rowWithoutGroup = CreateRow(RecipeTestDriver.WaitActionId);

		var (presenter, comboBox) = MaterializeComboBox(template, rowWithGroup);

		var items = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		comboBox.SelectedItem = items[0];

		var withGroupActions = 0;
		var withGroupPropertyChanges = 0;
		var withoutGroupActions = 0;
		var withoutGroupPropertyChanges = 0;
		rowWithGroup.ActionChanged += _ => withGroupActions++;
		rowWithGroup.PropertyValueChanged += (_, _) => withGroupPropertyChanges++;
		rowWithoutGroup.ActionChanged += _ => withoutGroupActions++;
		rowWithoutGroup.PropertyValueChanged += (_, _) => withoutGroupPropertyChanges++;

		RecycleOnto(presenter, rowWithoutGroup);

		withGroupActions.Should().Be(0, "previous row must not see writebacks during recycle");
		withGroupPropertyChanges.Should().Be(0, "previous row must not see writebacks during recycle");
		withoutGroupActions.Should().Be(0, "new row must not see phantom action mutations from the previous selection");
		withoutGroupPropertyChanges.Should().Be(0, "new row must not see phantom property mutations from the previous selection");
	}

	// Real recycling: the DataGrid hands the scrolled-out row's cell, with its pooled editor, to another row.
	[AvaloniaFact]
	public void ScrolledOutRow_RecycledCellPress_ShowsNewRowValue_WritesNoRow()
	{
		const string ActionColumn = "action";
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WithGroupActionId).IsSuccess.Should().BeTrue();
		for (var i = 1; i < RecycledStepCount; i++)
		{
			_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId).IsSuccess.Should().BeTrue();
		}

		using var grid = new CanonicalGridWindow(_fixture);
		var firstRow = grid.Surface.RecipeRows[0];
		var presenter = grid.PresenterAt(0, ActionColumn);
		grid.Click(presenter);
		grid.PressKey(Key.Escape, PhysicalKey.Escape);
		var pooledEditor = presenter.Editor;

		// The DataGrid keeps the row that last held keyboard focus realized, so focus leaves the grid first.
		grid.Window.FocusManager.Focus(null);
		Dispatcher.UIThread.RunJobs();
		grid.DataGrid.ScrollIntoView(grid.Surface.RecipeRows[^1], null);
		Dispatcher.UIThread.RunJobs();

		pooledEditor.Should().NotBeNull("precondition: the first press built the editor");
		var newRow = presenter.DataContext.Should().BeOfType<RecipeRowViewModel>(
			"precondition: the cell is still realized").Subject;
		newRow.Should().NotBeSameAs(firstRow, "precondition: the grid recycled the cell onto another row");
		presenter.IsEffectivelyVisible.Should().BeTrue("precondition: the recycled cell is on screen");
		var mutations = 0;
		void OnMutated(MutationSignal signal)
		{
			mutations++;
		}

		var rowWrites = 0;
		firstRow.PropertyValueChanged += (_, _) => rowWrites++;
		firstRow.ActionChanged += _ => rowWrites++;
		newRow.PropertyValueChanged += (_, _) => rowWrites++;
		newRow.ActionChanged += _ => rowWrites++;
		_fixture.Coordinator.Mutated += OnMutated;
		try
		{
			grid.Click(presenter);

			presenter.Editor.Should().BeSameAs(pooledEditor, "the press re-enters the pooled editor");
			var comboBox = (ComboBox)pooledEditor!;
			comboBox.IsDropDownOpen.Should().BeTrue("the press opens the list");
			((ComboBoxItemViewModel)comboBox.SelectedItem!).Id.Should().Be(
				RecipeTestDriver.WaitActionId,
				"the list selects the new row's value");
			grid.PressKey(Key.Escape, PhysicalKey.Escape);
		}
		finally
		{
			_fixture.Coordinator.Mutated -= OnMutated;
		}

		presenter.GetVisualDescendants().OfType<TextBlock>().First().Text.Should().Be(
			ActionText(RecipeTestDriver.WaitActionId),
			"the display shows the new row's value");
		mutations.Should().Be(0, "neither the recycle nor the re-entry writes the recipe");
		rowWrites.Should().Be(0, "neither row sees a write");
		_fixture.Coordinator.CurrentRecipe.Steps[0].ActionKey.Should().Be(RecipeTestDriver.WithGroupActionId);
	}

	private string ActionText(int actionId)
	{
		return _fixture.ComboBoxItemsProvider.ActionItems.Single(item => item.Id == actionId).DisplayText;
	}

	private IDataTemplate GetGroupColumnCellTemplate()
	{
		var columnBuilder = new ColumnBuilder(
			GridStyleOptions.Default, _fixture.RecipeMetadataRegistry, _fixture.ComboBoxItemsProvider);
		var grid = new DataGrid();
		columnBuilder.BuildColumns(grid, new CellEditCoordinator());

		var groupColumn = grid.Columns
			.OfType<DataGridTemplateColumn>()
			.FirstOrDefault(column => string.Equals(column.Tag as string, RecipeTestDriver.TargetColumn, StringComparison.Ordinal));

		groupColumn.Should().NotBeNull("the target column must exist for the WithGroups configuration");
		groupColumn.CellTemplate.Should().NotBeNull("the group ComboBox must materialize from CellTemplate");

		return groupColumn.CellTemplate;
	}

	private RecipeRowViewModel CreateRow(int actionId)
	{
		var action = _fixture.RecipeMetadataRegistry.GetAction(actionId).Value;
		var step = new Step(actionId, []);
		var inapplicableColumns = BuildInapplicableColumns(action, step);
		return new RecipeRowViewModel(
			1, step, action, _fixture.RecipeMetadataRegistry, _fixture.ComboBoxItemsProvider, inapplicableColumns);
	}

	private IReadOnlySet<string> BuildInapplicableColumns(ActionDefinition action, Step step)
	{
		var activeColumnKeys = ActiveColumnSetResolver.Resolve(action, step);
		var inapplicable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in _fixture.RecipeMetadataRegistry.GetAllColumns())
		{
			if (CellStateResolver.IsInapplicable(column, activeColumnKeys))
			{
				inapplicable.Add(column.Key);
			}
		}
		return inapplicable;
	}

	// The cell template builds a display cell; its editor exists only after an edit entry. Outside a DataGrid
	// the lock leg fails closed, so the test enables the cell. The editor stays in the cell and takes its row
	// from the cell, as in the grid.
	private static (LazyComboCellPresenter Presenter, ComboBox ComboBox) MaterializeComboBox(
		IDataTemplate template,
		RecipeRowViewModel row)
	{
		var presenter = template.Build(row) as LazyComboCellPresenter;
		presenter.Should().NotBeNull("the CellTemplate builds a lazy combo cell");
		presenter.IsEnabled = true;
		presenter.DataContext = row;
		presenter.EnterEdit(initialText: null);
		var comboBox = presenter.Editor as ComboBox;
		comboBox.Should().NotBeNull("an edit entry builds the ComboBox editor");

		return (presenter, comboBox);
	}

	// What a recycled row does to the cell: the row change ends the edit, and the next entry re-enters the
	// pooled editor on the new row. ScrolledOutRow_RecycledCellPress_ShowsNewRowValue_WritesNoRow drives the
	// same path through a scrolled DataGrid.
	private static void RecycleOnto(LazyComboCellPresenter presenter, RecipeRowViewModel row)
	{
		presenter.DataContext = row;
		presenter.IsEditing.Should().BeFalse("the row change ends the edit");
		presenter.EnterEdit(initialText: null);
	}
}
