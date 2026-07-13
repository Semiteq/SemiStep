using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class ParameterCellViewModelTests : IAsyncLifetime
{
	private const string ActionColumn = "action";

	private readonly UIFixture _fixture = new();

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void Factory_MapsColumnTypesToCellKinds()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var factory = CreateFactory();

		factory.Create(row, FindDescriptor(ActionColumn)).Should().BeOfType<ActionComboBoxCellViewModel>();
		factory.Create(row, FindDescriptor(RecipeTestDriver.TargetColumn)).Should().BeOfType<TargetComboBoxCellViewModel>();
		factory.Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn)).Should().BeOfType<PropertyTextCellViewModel>();
		factory.Create(row, FindDescriptor(RecipeTestDriver.TaskColumn)).Should().BeOfType<PropertyTextCellViewModel>();
		factory.Create(row, FindDescriptor(RecipeTestDriver.CommentColumn)).Should().BeOfType<PropertyTextCellViewModel>();
	}

	[AvaloniaFact]
	public void Factory_ReadOnlyAndStepStartTimeParameters_YieldReadOnlyCells()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var factory = CreateFactory();
		var readOnlyText = new ParameterDescriptor(
			RecipeTestDriver.CommentColumn, "Comment", ColumnTypes.TextField, IsReadOnlyParameter: true);
		var stepStartTime = new ParameterDescriptor(
			TimeFormatHelper.StepStartTimeColumnKey, "Start", ColumnTypes.StepStartTimeField, IsReadOnlyParameter: false);

		factory.Create(row, readOnlyText).Should().BeOfType<ReadOnlyCellViewModel>();
		factory.Create(row, stepStartTime).Should().BeOfType<ReadOnlyCellViewModel>();
	}

	[AvaloniaFact]
	public void ActionCell_ExposesRegistryItems_AndActionIdValue()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = (ActionComboBoxCellViewModel)CreateFactory().Create(row, FindDescriptor(ActionColumn));

		cell.Items.Should().BeEquivalentTo(_fixture.RecipeMetadataRegistry.GetActionComboBoxItems());
		cell.Value.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void ActionCell_Write_RaisesActionChangedWithParsedId()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(ActionColumn));
		var actionChanges = new List<int>();
		var propertyWrites = new List<(string Key, string? Value)>();
		row.ActionChanged += actionId => actionChanges.Add(actionId);
		row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));

		cell.Value = RecipeTestDriver.ForLoopActionId.ToString();

		actionChanges.Should().Equal(RecipeTestDriver.ForLoopActionId);
		propertyWrites.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ActionCell_WriteSameAction_RaisesNothing()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(ActionColumn));
		var actionChanges = new List<int>();
		row.ActionChanged += actionId => actionChanges.Add(actionId);

		cell.Value = RecipeTestDriver.WaitActionId.ToString();

		actionChanges.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void TargetCell_ItemsComeFromRowGroupItems()
	{
		var valveRow = CreateRow(RecipeTestDriver.WithGroupActionId);
		var waitRow = CreateRow(RecipeTestDriver.WaitActionId);
		var factory = CreateFactory();
		var descriptor = FindDescriptor(RecipeTestDriver.TargetColumn);

		var valveCell = (TargetComboBoxCellViewModel)factory.Create(valveRow, descriptor);
		var waitCell = (TargetComboBoxCellViewModel)factory.Create(waitRow, descriptor);

		valveCell.Items.Should().HaveCount(2);
		valveCell.Items.Select(item => item.DisplayText).Should().BeEquivalentTo("Open", "Close");
		waitCell.Items.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void TargetCell_Write_RaisesPropertyValueChanged()
	{
		var row = CreateRow(RecipeTestDriver.WithGroupActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(RecipeTestDriver.TargetColumn));
		var propertyWrites = new List<(string Key, string? Value)>();
		row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));

		cell.Value = "2";

		propertyWrites.Should().Equal((RecipeTestDriver.TargetColumn, "2"));
	}

	[AvaloniaFact]
	public void PropertyTextCell_ValueRoundTrip_RaisesInpc()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn));
		var propertyWrites = new List<(string Key, string? Value)>();
		var changedProperties = new List<string?>();
		row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));
		cell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		cell.Value = "25";
		propertyWrites.Should().Equal((RecipeTestDriver.StepDurationColumn, "25"));

		_fixture.Session.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "25");
		row.UpdateStep(_fixture.Coordinator.CurrentRecipe.Steps[0]);

		changedProperties.Should().Contain(nameof(ParameterCellViewModel.Value));
		cell.Value.Should().Be(25f);
	}

	[AvaloniaFact]
	public void Cell_IsApplicable_TracksRowInapplicableColumns()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var factory = CreateFactory();
		var durationCell = factory.Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn));
		var taskCell = factory.Create(row, FindDescriptor(RecipeTestDriver.TaskColumn));
		var changedProperties = new List<string?>();
		taskCell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		durationCell.IsApplicable.Should().BeTrue();
		taskCell.IsApplicable.Should().BeFalse();

		row.RecomputeInapplicableColumns();

		changedProperties.Should().NotContain(
			nameof(ParameterCellViewModel.IsApplicable),
			"a recompute that leaves the applicability set unchanged must not re-fire cell notifications");
	}

	[AvaloniaFact]
	public void Cell_IsChanged_TracksRowChangedColumns()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn));
		var changedProperties = new List<string?>();
		cell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		row.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		cell.IsChanged.Should().BeTrue();

		row.ClearChanged(RecipeTestDriver.StepDurationColumn);
		cell.IsChanged.Should().BeFalse();

		changedProperties.Should().Contain(nameof(ParameterCellViewModel.IsChanged));
	}

	[AvaloniaFact]
	public void Cell_FormatKindAndUnits_SurfaceForTemplateConverters()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var factory = CreateFactory();
		var durationCell = factory.Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn));
		var commentCell = factory.Create(row, FindDescriptor(RecipeTestDriver.CommentColumn));
		var taskCell = factory.Create(row, FindDescriptor(RecipeTestDriver.TaskColumn));

		durationCell.FormatKind.Should().Be(TimeFormatHelper.TimeHmsFormat);
		durationCell.Units.Should().Be("s");
		commentCell.FormatKind.Should().Be(TimeFormatHelper.DefaultFormatKind);
		taskCell.FormatKind.Should().Be(TimeFormatHelper.DefaultFormatKind);
		taskCell.Units.Should().BeNull();
	}

	[AvaloniaFact]
	public void StepStartTimeCell_ValueFollowsRowStepStartTime()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var descriptor = new ParameterDescriptor(
			TimeFormatHelper.StepStartTimeColumnKey, "Start", ColumnTypes.StepStartTimeField, IsReadOnlyParameter: true);
		var cell = CreateFactory().Create(row, descriptor);
		var changedProperties = new List<string?>();
		cell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		row.UpdateStepStartTime("00:01:40");

		changedProperties.Should().Contain(nameof(ParameterCellViewModel.Value));
		cell.Value.Should().Be("00:01:40");
	}

	[AvaloniaFact]
	public void ReadOnlyCell_Write_IsIgnored()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var descriptor = new ParameterDescriptor(
			RecipeTestDriver.CommentColumn, "Comment", ColumnTypes.TextField, IsReadOnlyParameter: true);
		var cell = CreateFactory().Create(row, descriptor);
		var propertyWrites = new List<(string Key, string? Value)>();
		row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));

		cell.Value = "ignored";

		propertyWrites.Should().BeEmpty();
	}

	[AvaloniaFact]
	public async Task SelectorWrite_RaisesSelectorValueChangedWithSeedAndDrop()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync("Standalone/NestedActionsValid");
		var registry = services.GetRequiredService<RecipeMetadataRegistry>();
		session.AppendStep(300);
		var step = session.Current.Steps[0];
		var action = registry.GetAction(step.ActionKey).Value;
		var row = new RecipeRowViewModel(
			1, step, action, registry,
			RecipeRowViewModel.BuildInapplicableColumns(action, step, registry));
		var descriptor = ParameterDescriptor.BuildFromRegistry(registry)
			.Single(candidate => candidate.ParameterKey == "branch_sel");
		var cell = new ParameterCellViewModelFactory(registry).Create(row, descriptor);
		var selectorEdits = new List<SelectorEdit>();
		var propertyWrites = new List<(string Key, string? Value)>();
		row.SelectorValueChanged += edit => selectorEdits.Add(edit);
		row.PropertyValueChanged += (key, value) => propertyWrites.Add((key, value));

		cell.Value = "1";

		selectorEdits.Should().ContainSingle();
		selectorEdits[0].SelectorKey.Should().Be("branch_sel");
		selectorEdits[0].Value.Should().Be("1");
		selectorEdits[0].ColumnsToSeed.Should().Contain("sub_value");
		propertyWrites.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void Cell_Dispose_StopsRowNotifications()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var cell = CreateFactory().Create(row, FindDescriptor(RecipeTestDriver.StepDurationColumn));
		var changedProperties = new List<string?>();
		cell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		cell.Dispose();
		row.MarkChanged([RecipeTestDriver.StepDurationColumn]);
		row.UpdateStep(_fixture.Coordinator.CurrentRecipe.Steps[0]);

		changedProperties.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ColumnDispose_CascadesToCells()
	{
		_fixture.SeedRecipe(1);
		var step = _fixture.Coordinator.CurrentRecipe.Steps[0];
		var action = _fixture.RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var factory = CreateFactory();
		var column = new StepColumnViewModel(
			1,
			step,
			action,
			_fixture.RecipeMetadataRegistry,
			ParameterDescriptor.BuildFromRegistry(_fixture.RecipeMetadataRegistry),
			factory.Create);
		var changedProperties = new List<string?>();
		foreach (var cell in column.Cells)
		{
			cell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);
		}

		column.Dispose();
		column.Row.MarkChanged([RecipeTestDriver.StepDurationColumn]);

		changedProperties.Should().BeEmpty();
	}

	private RecipeRowViewModel CreateRow(int actionId)
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(actionId);
		var step = _fixture.Coordinator.CurrentRecipe.Steps[0];
		var action = _fixture.RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(
			action, step, _fixture.RecipeMetadataRegistry);

		return new RecipeRowViewModel(1, step, action, _fixture.RecipeMetadataRegistry, inapplicableColumns);
	}

	private ParameterCellViewModelFactory CreateFactory()
	{
		return new ParameterCellViewModelFactory(_fixture.RecipeMetadataRegistry);
	}

	private ParameterDescriptor FindDescriptor(string parameterKey)
	{
		return ParameterDescriptor.BuildFromRegistry(_fixture.RecipeMetadataRegistry)
			.Single(descriptor => descriptor.ParameterKey == parameterKey);
	}
}
