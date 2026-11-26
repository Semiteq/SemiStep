using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class ColumnBuilderIdempotencyTests : IAsyncLifetime
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
	public void BuildColumns_CalledTwice_DoesNotAccumulateColumns()
	{
		var columnBuilder = new ColumnBuilder(
			GridStyleOptions.Default, _fixture.RecipeMetadataRegistry, _fixture.ComboBoxItemsProvider);
		var grid = new DataGrid();

		columnBuilder.BuildColumns(grid, new CellEditCoordinator());
		var firstCount = grid.Columns.Count;

		columnBuilder.BuildColumns(grid, new CellEditCoordinator());
		var secondCount = grid.Columns.Count;

		secondCount.Should().Be(firstCount);
	}

	[AvaloniaFact]
	public void BuildColumns_ActionColumn_HasCellTemplateOnlyAndIsReadOnly()
	{
		var grid = BuildGrid();

		var actionColumn = FindTemplateColumnByTag(grid, ColumnTypes.Action);

		actionColumn.Should().NotBeNull("the action column must exist for the recipe grid");
		actionColumn!.IsReadOnly.Should().BeTrue(
			"ComboBox cells live in CellTemplate and DataGrid must never enter edit mode for the action column");
		actionColumn.CellTemplate.Should().NotBeNull("the action ComboBox must materialize from CellTemplate");
		actionColumn.CellEditingTemplate.Should().BeNull(
			"the Avalonia 12 broken CellEditingTemplate path must not be wired up");
		actionColumn.CellTheme.Should().NotBeNull(
			"the recipe cell theme must be attached so the cell takes its colour from the palette");
	}

	[AvaloniaFact]
	public void BuildColumns_AllColumns_HaveTheRecipeCellTheme()
	{
		var grid = BuildGrid();

		foreach (var column in grid.Columns.OfType<DataGridTemplateColumn>())
		{
			column.CellTheme.Should().NotBeNull(
				$"column {column.Tag} must have the recipe cell theme so its cells take their colour from the palette");
		}
	}

	[AvaloniaFact]
	public void BuildColumns_GroupComboBoxColumn_HasCellTemplateOnlyAndIsReadOnly()
	{
		var grid = BuildGrid();

		var groupColumn = FindTemplateColumnByTag(grid, RecipeTestDriver.TargetColumn);

		groupColumn.Should().NotBeNull("the target column must exist for the WithGroups configuration");
		groupColumn!.IsReadOnly.Should().BeTrue(
			"ComboBox cells live in CellTemplate and DataGrid must never enter edit mode for group columns");
		groupColumn.CellTemplate.Should().NotBeNull("the group ComboBox must materialize from CellTemplate");
		groupColumn.CellEditingTemplate.Should().BeNull(
			"the Avalonia 12 broken CellEditingTemplate path must not be wired up");
	}

	[AvaloniaFact]
	public void BuildColumns_ReadOnlyColumn_HasReadOnlyColumnClass()
	{
		var registry = BuildRegistryWithReadOnlyVariants();
		var columnBuilder = new ColumnBuilder(GridStyleOptions.Default, registry, new ComboBoxItemsProvider(registry));
		var grid = new DataGrid();

		columnBuilder.BuildColumns(grid, new CellEditCoordinator());

		var readOnlyColumn = grid.Columns
			.First(c => string.Equals(c.Tag as string, "ro_column", StringComparison.Ordinal));
		var editableColumn = grid.Columns
			.First(c => string.Equals(c.Tag as string, "edit_column", StringComparison.Ordinal));

		readOnlyColumn.CellStyleClasses.Should().Contain(
			"read-only-column",
			"a column with ReadOnly=true must carry the read-only-column class so the disabled style fires");
		editableColumn.CellStyleClasses.Should().NotContain(
			"read-only-column",
			"a column with ReadOnly=false must not carry the read-only-column class");
	}

	private static RecipeMetadataRegistry BuildRegistryWithReadOnlyVariants()
	{
		var stringProperty = TestPropertyTypeDefinitionBuilder.CreateString(
			"string",
			TestRecipeMetadataRegistryFactory.DefaultStringMaxLength);

		var columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["ro_column"] = new GridColumnDefinition(
				Key: "ro_column",
				ColumnType: "property_field",
				UiName: "Read Only",
				PropertyTypeId: "string",
				ReadOnly: true,
				SaveToCsv: true),
			["edit_column"] = new GridColumnDefinition(
				Key: "edit_column",
				ColumnType: "property_field",
				UiName: "Editable",
				PropertyTypeId: "string",
				ReadOnly: false,
				SaveToCsv: true),
		};

		return TestRecipeMetadataRegistryFactory.Build(
			properties: [stringProperty],
			columns: columns);
	}

	[AvaloniaFact]
	public void ActionCellTemplate_SettingSelectedItemOnComboBox_FiresActionChanged()
	{
		// Manual verification required: hit-testing (clicking the dropdown to open it) is not
		// observable in Avalonia headless mode. This test covers the TwoWay binding contract by
		// directly mutating SelectedItem on the materialized ComboBox and asserting that the
		// ActionChanged event fires with the expected id.
		var grid = BuildGrid();
		var actionColumn = FindTemplateColumnByTag(grid, ColumnTypes.Action);
		actionColumn.Should().NotBeNull();

		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		var comboBox = MaterializeComboBox(actionColumn!.CellTemplate!, row);

		var forLoopItem = comboBox.ItemsSource!
			.Cast<ComboBoxItemViewModel>()
			.Single(item => item.Id == RecipeTestDriver.ForLoopActionId);

		comboBox.SelectedItem = forLoopItem;

		receivedActionId.Should().Be(
			RecipeTestDriver.ForLoopActionId,
			"selecting a different action item must propagate through SelectionChanged to ActionChanged");
	}

	private DataGrid BuildGrid()
	{
		var columnBuilder = new ColumnBuilder(
			GridStyleOptions.Default, _fixture.RecipeMetadataRegistry, _fixture.ComboBoxItemsProvider);
		var grid = new DataGrid();
		columnBuilder.BuildColumns(grid, new CellEditCoordinator());
		return grid;
	}

	private static DataGridTemplateColumn? FindTemplateColumnByTag(DataGrid grid, string tag)
	{
		return grid.Columns
			.OfType<DataGridTemplateColumn>()
			.FirstOrDefault(column => string.Equals(column.Tag as string, tag, StringComparison.Ordinal));
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

	// Outside a DataGrid the lock leg fails closed, so the test enables the cell and enters its edit once.
	private static ComboBox MaterializeComboBox(IDataTemplate template, RecipeRowViewModel row)
	{
		var presenter = template.Build(row) as LazyComboCellPresenter;
		presenter.Should().NotBeNull("the CellTemplate builds a lazy combo cell");
		presenter.IsEnabled = true;
		presenter.DataContext = row;
		presenter.EnterEdit(initialText: null);
		var comboBox = presenter.Editor as ComboBox;
		comboBox.Should().NotBeNull("an edit entry builds the ComboBox editor");

		return comboBox;
	}
}
