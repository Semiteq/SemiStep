using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

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

		var comboBox = MaterializeComboBox(template, rowWithGroup);

		var items = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>(
				"the group column's ItemsSource must bind to the row VM's per-column group items list").Subject;

		items.Should().NotBeEmpty(
			"action 50 (Valve) defines a target column bound to the 'valve' group which has items");

		items.Select(item => item.DisplayText).Should().Contain(new[] { "Open", "Close" });
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

		var comboBox = MaterializeComboBox(template, rowWithoutGroup);

		comboBox.ItemsSource.Should().NotBeNull(
			"target column is pre-populated with an empty list on every row so binding never sees UnsetValue");
		var initialItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IEnumerable<ComboBoxItemViewModel>>().Subject;
		initialItems.Should().BeEmpty(
			"Wait action does not declare a 'target' property, so the pre-populated list is empty");

		// Simulate Avalonia recycling the same visual onto a different row.
		comboBox.DataContext = rowWithGroup;

		var refreshedItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>(
				"DataContext swap must propagate through the ItemsSource binding to the new row's group items").Subject;

		refreshedItems.Should().NotBeEmpty(
			"after swapping DataContext to a Valve-action row, the ComboBox must surface the valve group items");

		refreshedItems.Select(item => item.DisplayText).Should().Contain(new[] { "Open", "Close" });
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

		var comboBox = MaterializeComboBox(template, rowWithGroup);

		comboBox.ItemsSource.Should().NotBeNull();

		comboBox.DataContext = rowWithoutGroup;

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
			properties: new[]
			{
				new ActionPropertyDefinition(
					Key: RecipeTestDriver.TargetColumn,
					GroupName: "first_group",
					PropertyTypeId: "enum",
					DefaultValue: null),
			});
		var secondAction = new ActionDefinition(
			id: 9002,
			uiName: "SecondGroupAction",
			deployDuration: DeployDuration.Immediate,
			properties: new[]
			{
				new ActionPropertyDefinition(
					Key: RecipeTestDriver.TargetColumn,
					GroupName: "second_group",
					PropertyTypeId: "enum",
					DefaultValue: null),
			});

		var registry = BuildTwoGroupRegistry();
		var firstRow = new RecipeRowViewModel(
			1,
			new Step(9001, ImmutableDictionary<PropertyId, PropertyValue>.Empty),
			firstAction,
			registry,
			new HashSet<string>());
		var secondRow = new RecipeRowViewModel(
			2,
			new Step(9002, ImmutableDictionary<PropertyId, PropertyValue>.Empty),
			secondAction,
			registry,
			new HashSet<string>());

		// Re-resolve the template against the synthetic registry. The CellTemplate is wired via
		// `Binding(GroupItemsByColumn[target])` which queries the row VM, so the registry used
		// by ColumnBuilder for template construction does not need to know about these synthetic
		// groups — but the registry consulted by RecipeRowViewModel does. Materialising directly
		// against the existing template with the synthetic row VMs exercises that exact path.
		var comboBox = MaterializeComboBox(template, firstRow);

		var firstItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		firstItems.Select(item => item.DisplayText).Should().BeEquivalentTo("alpha", "beta");

		comboBox.DataContext = secondRow;

		var secondItems = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		secondItems.Select(item => item.DisplayText).Should().BeEquivalentTo("gamma", "delta");
	}

	private static RecipeMetadataRegistry BuildTwoGroupRegistry()
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

		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: columns,
			Groups: groups,
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

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

		var comboBox = MaterializeComboBox(template, row);

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
		captured[0].Value.Should().Be(chosen.Id.ToString());
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

		var comboBox = MaterializeComboBox(template, rowWithGroup);

		var items = comboBox.ItemsSource
			.Should().BeAssignableTo<IReadOnlyList<ComboBoxItemViewModel>>().Subject;
		comboBox.SelectedItem = items.First();

		var withGroupActions = 0;
		var withGroupPropertyChanges = 0;
		var withoutGroupActions = 0;
		var withoutGroupPropertyChanges = 0;
		rowWithGroup.ActionChanged += _ => withGroupActions++;
		rowWithGroup.PropertyValueChanged += (_, _) => withGroupPropertyChanges++;
		rowWithoutGroup.ActionChanged += _ => withoutGroupActions++;
		rowWithoutGroup.PropertyValueChanged += (_, _) => withoutGroupPropertyChanges++;

		comboBox.DataContext = rowWithoutGroup;

		withGroupActions.Should().Be(0, "previous row must not see writebacks during recycle");
		withGroupPropertyChanges.Should().Be(0, "previous row must not see writebacks during recycle");
		withoutGroupActions.Should().Be(0, "new row must not see phantom action mutations from the previous selection");
		withoutGroupPropertyChanges.Should().Be(0, "new row must not see phantom property mutations from the previous selection");
	}

	private IDataTemplate GetGroupColumnCellTemplate()
	{
		var columnBuilder = new ColumnBuilder(GridStyleOptions.Default, _fixture.RecipeMetadataRegistry);
		var grid = new DataGrid();
		columnBuilder.BuildColumns(grid);

		var groupColumn = grid.Columns
			.OfType<DataGridTemplateColumn>()
			.FirstOrDefault(column => string.Equals(column.Tag as string, RecipeTestDriver.TargetColumn, StringComparison.Ordinal));

		groupColumn.Should().NotBeNull("the target column must exist for the WithGroups configuration");
		groupColumn!.CellTemplate.Should().NotBeNull("the group ComboBox must materialize from CellTemplate");

		return groupColumn.CellTemplate!;
	}

	private RecipeRowViewModel CreateRow(int actionId)
	{
		var action = _fixture.RecipeMetadataRegistry.GetAction(actionId).Value;
		var step = new Step(actionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty);
		var inapplicableColumns = BuildInapplicableColumns(action);
		return new RecipeRowViewModel(1, step, action, _fixture.RecipeMetadataRegistry, inapplicableColumns);
	}

	private IReadOnlySet<string> BuildInapplicableColumns(ActionDefinition action)
	{
		var inapplicable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in _fixture.RecipeMetadataRegistry.GetAllColumns())
		{
			if (CellStateResolver.IsInapplicable(column, action))
			{
				inapplicable.Add(column.Key);
			}
		}
		return inapplicable;
	}

	private static ComboBox MaterializeComboBox(IDataTemplate template, RecipeRowViewModel row)
	{
		var built = template.Build(row);
		built.Should().NotBeNull();

		var comboBox = built as ComboBox;
		comboBox.Should().NotBeNull("CellTemplate must return a ComboBox directly");
		comboBox!.DataContext = row;
		return comboBox;
	}
}
