using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Tests.Core.Helpers;

using UI.RecipeGrid;

using Xunit;

namespace Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeRow")]
[Trait("Category", "Integration")]
public sealed class RecipeRowViewModelTests : IAsyncLifetime
{
	private RecipeWorkspace _workspace = null!;
	private RecipeEditor _editor = null!;
	private ConfigRegistry _configRegistry = null!;

	public async Task InitializeAsync()
	{
		var (services, workspace, editor, _) = await CoreTestHelper.BuildAsync("WithGroups");
		_workspace = workspace;
		_editor = editor;
		_configRegistry = services.GetRequiredService<ConfigRegistry>();
	}

	public Task DisposeAsync()
	{
		return Task.CompletedTask;
	}

	private RecipeRowViewModel CreateRow(int actionId = RecipeTestDriver.WaitActionId)
	{
		_editor.AppendStep(actionId);
		var step = _workspace.CurrentRecipe.Steps[0];
		var action = _configRegistry.GetAction(step.ActionKey).Value;
		var cellStates = BuildCellStates(action);
		return new RecipeRowViewModel(1, step, action, _configRegistry, cellStates);
	}

	private IReadOnlyDictionary<string, CellState> BuildCellStates(ActionDefinition action)
	{
		var states = new Dictionary<string, CellState>();
		foreach (var col in _configRegistry.GetAllColumns())
		{
			states[col.Key] = CellStateResolver.GetCellState(col, action);
		}
		return states;
	}

	[Fact]
	public void GetPropertyValue_Action_ReturnsActionId()
	{
		_workspace.Reset();
		var row = CreateRow();

		var value = row.GetPropertyValue("action");

		value.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[Fact]
	public void GetPropertyValue_StepStartTime_ReturnsNull_Initially()
	{
		_workspace.Reset();
		var row = CreateRow();

		var value = row.GetPropertyValue("step_start_time");

		value.Should().BeNull();
	}

	[Fact]
	public void GetPropertyValue_UnknownKey_ReturnsNull()
	{
		_workspace.Reset();
		var row = CreateRow();

		var value = row.GetPropertyValue("nonexistent_column");

		value.Should().BeNull();
	}

	[Fact]
	public void GetPropertyValue_KnownColumn_ReturnsPropertyValue()
	{
		_workspace.Reset();
		var row = CreateRow();

		var value = row.GetPropertyValue(RecipeTestDriver.StepDurationColumn);

		value.Should().NotBeNull();
	}

	[Fact]
	public void Indexer_Get_DelegatesToGetPropertyValue()
	{
		_workspace.Reset();
		var row = CreateRow();

		var indexerValue = row["action"];
		var getterValue = row.GetPropertyValue("action");

		indexerValue.Should().Be(getterValue);
	}

	[Fact]
	public void SetPropertyValue_Action_FiresActionChangedEvent()
	{
		_workspace.Reset();
		var row = CreateRow();
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		row.SetPropertyValue("action", RecipeTestDriver.ForLoopActionId.ToString());

		receivedActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[Fact]
	public void SetPropertyValue_Action_InvalidValue_DoesNotFireEvent()
	{
		_workspace.Reset();
		var row = CreateRow();
		var eventFired = false;
		row.ActionChanged += _ => eventFired = true;

		row.SetPropertyValue("action", "notanumber");

		eventFired.Should().BeFalse();
	}

	[Fact]
	public void SetPropertyValue_NonAction_FiresPropertyValueChangedEvent()
	{
		_workspace.Reset();
		var row = CreateRow();
		var receivedColumnKey = string.Empty;
		row.PropertyValueChanged += (key, _) => receivedColumnKey = key;

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "5");

		receivedColumnKey.Should().Be(RecipeTestDriver.StepDurationColumn);
	}

	[Fact]
	public void UpdateStep_RaisesItemArrayPropertyChanged()
	{
		_workspace.Reset();
		var row = CreateRow();
		var changedProperties = new List<string>();
		row.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

		var updatedStep = _workspace.CurrentRecipe.Steps[0];
		row.UpdateStep(updatedStep);

		changedProperties.Should().Contain("Item[]");
	}

	[Fact]
	public void UpdateStepNumber_ChangesStepNumber()
	{
		_workspace.Reset();
		var row = CreateRow();

		row.UpdateStepNumber(3);

		row.StepNumber.Should().Be(3);
	}

	[Fact]
	public void UpdateStepStartTime_ChangesStepStartTime()
	{
		_workspace.Reset();
		var row = CreateRow();

		row.UpdateStepStartTime("123.5");

		row.StepStartTime.Should().Be("123.5");
	}

	[Fact]
	public void CellStates_NotEmpty()
	{
		_workspace.Reset();
		var row = CreateRow();

		row.CellStates.Count.Should().BeGreaterThan(0);
	}

	[Fact]
	public void CellStates_ActionColumn_IsEnabled()
	{
		_workspace.Reset();
		var row = CreateRow(RecipeTestDriver.PauseActionId);

		row.CellStates["action"].Should().Be(CellState.Enabled);
	}

	[Fact]
	public void GetGroupNameForColumn_ReturnsNull_ForNonGroupColumn()
	{
		_workspace.Reset();
		var row = CreateRow();

		var groupName = row.GetGroupNameForColumn(RecipeTestDriver.StepDurationColumn);

		groupName.Should().BeNull();
	}

	[Fact]
	public void GetGroupItemsForColumn_ReturnsNull_ForNonGroupColumn()
	{
		_workspace.Reset();
		var row = CreateRow();

		var groupItems = row.GetGroupItemsForColumn(RecipeTestDriver.StepDurationColumn);

		groupItems.Should().BeNull();
	}

	[Fact]
	public void ColumnUnits_ContainsStepStartTime_WithTimeUnits()
	{
		_workspace.Reset();
		var row = CreateRow();

		row.ColumnUnits.Should().ContainKey("step_start_time")
			.WhoseValue.Should().Be("с");
	}

	[Fact]
	public void ColumnFormatKinds_ContainsStepStartTime_WithTimeHmsFormat()
	{
		_workspace.Reset();
		var row = CreateRow();

		row.ColumnFormatKinds.Should().ContainKey("step_start_time")
			.WhoseValue.Should().Be("time_hms");
	}

	[Fact]
	public void ColumnUnits_ContainsActionColumnUnits()
	{
		_workspace.Reset();
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		// Wait action: step_duration -> time property -> units "s"
		row.ColumnUnits.Should().ContainKey(RecipeTestDriver.StepDurationColumn)
			.WhoseValue.Should().Be("s");
	}

	[Fact]
	public void ColumnFormatKinds_ContainsActionColumnFormatKind()
	{
		_workspace.Reset();
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		// Wait action: step_duration -> time property -> formatKind "time_hms"
		row.ColumnFormatKinds.Should().ContainKey(RecipeTestDriver.StepDurationColumn)
			.WhoseValue.Should().Be("time_hms");
	}

	[Fact]
	public void ColumnUnits_ColumnWithEmptyUnits_ReturnsEmptyString()
	{
		_workspace.Reset();
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		// Wait action: comment -> string property -> units ""
		row.ColumnUnits.Should().ContainKey(RecipeTestDriver.CommentColumn)
			.WhoseValue.Should().Be("");
	}

	[Fact]
	public void Dispose_NullsEventDelegates()
	{
		_workspace.Reset();
		var row = CreateRow();
		var handlerCalled = false;
		row.PropertyValueChanged += (_, _) => handlerCalled = true;

		row.Dispose();
		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "5");

		handlerCalled.Should().BeFalse();
	}
}
