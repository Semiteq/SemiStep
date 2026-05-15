using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeRow")]
[Trait("Category", "Integration")]
public sealed class RecipeRowViewModelTests : IAsyncLifetime
{
	private RecipeWorkspace _workspace = null!;
	private RecipeEditor _editor = null!;
	private RecipeMetadataRegistry _recipeMetadataRegistry = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, workspace, editor, _) = await CoreTestHelper.BuildAsync("WithGroups");
		_workspace = workspace;
		_editor = editor;
		_recipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		_workspace.Reset();
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}

	private RecipeRowViewModel CreateRow(int actionId = RecipeTestDriver.WaitActionId)
	{
		_editor.AppendStep(actionId);
		var step = _workspace.CurrentRecipe.Steps[0];
		var action = _recipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var cellStates = BuildCellStates(action);
		return new RecipeRowViewModel(1, step, action, _recipeMetadataRegistry, cellStates);
	}

	private IReadOnlyDictionary<string, CellState> BuildCellStates(ActionDefinition action)
	{
		var states = new Dictionary<string, CellState>(StringComparer.OrdinalIgnoreCase);
		foreach (var col in _recipeMetadataRegistry.GetAllColumns())
		{
			states[col.Key] = CellStateResolver.GetCellState(col, action);
		}
		return states;
	}

	[AvaloniaFact]
	public void GetPropertyValue_Action_ReturnsActionId()
	{
		var row = CreateRow();

		var value = row.GetPropertyValue("action");

		value.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void GetPropertyValue_StepStartTime_ReturnsNull_Initially()
	{
		var row = CreateRow();

		var value = row.GetPropertyValue("step_start_time");

		value.Should().BeNull();
	}

	[AvaloniaFact]
	public void GetPropertyValue_UnknownKey_ReturnsNull()
	{
		var row = CreateRow();

		var value = row.GetPropertyValue("nonexistent_column");

		value.Should().BeNull();
	}

	[AvaloniaFact]
	public void GetPropertyValue_KnownColumn_ReturnsPropertyValue()
	{
		var row = CreateRow();

		var value = row.GetPropertyValue(RecipeTestDriver.StepDurationColumn);

		value.Should().NotBeNull();
	}

	[AvaloniaFact]
	public void Indexer_Get_DelegatesToGetPropertyValue()
	{
		var row = CreateRow();

		var indexerValue = row["action"];
		var directValue = row.GetPropertyValue("action");

		indexerValue.Should().Be(directValue);
		indexerValue.Should().Be(RecipeTestDriver.WaitActionId);
	}

	[AvaloniaFact]
	public void Indexer_Get_UnknownKey_ReturnsNull()
	{
		var row = CreateRow();

		var value = row["nonexistent_column"];

		value.Should().BeNull();
	}

	[AvaloniaFact]
	public void SetPropertyValue_Action_FiresActionChangedEvent()
	{
		var row = CreateRow();
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		row.SetPropertyValue("action", RecipeTestDriver.ForLoopActionId.ToString());

		receivedActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void Indexer_SetActionToIntId_FiresActionChangedEvent()
	{
		var row = CreateRow();
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		row["action"] = RecipeTestDriver.ForLoopActionId;

		receivedActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void SetPropertyValue_Action_InvalidValue_DoesNotFireEvent()
	{
		var row = CreateRow();
		var eventFired = false;
		row.ActionChanged += _ => eventFired = true;

		row.SetPropertyValue("action", "notanumber");

		eventFired.Should().BeFalse();
	}

	[AvaloniaFact]
	public void SetPropertyValue_NonAction_FiresPropertyValueChangedEvent()
	{
		var row = CreateRow();
		var receivedColumnKey = string.Empty;
		row.PropertyValueChanged += (key, _) => receivedColumnKey = key;

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "5");

		receivedColumnKey.Should().Be(RecipeTestDriver.StepDurationColumn);
	}

	[AvaloniaFact]
	public void UpdateStep_RaisesItemArrayPropertyChanged()
	{
		var row = CreateRow();
		var changedProperties = new List<string>();
		row.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

		var updatedStep = _workspace.CurrentRecipe.Steps[0];
		row.UpdateStep(updatedStep);

		changedProperties.Should().Contain("Item[]");
	}

	[AvaloniaFact]
	public void UpdateStepNumber_ChangesStepNumber()
	{
		var row = CreateRow();

		row.UpdateStepNumber(3);

		row.StepNumber.Should().Be(3);
	}

	[AvaloniaFact]
	public void UpdateStepStartTime_ChangesStepStartTime()
	{
		var row = CreateRow();

		row.UpdateStepStartTime("123.5");

		row.StepStartTime.Should().Be("123.5");
	}

	[AvaloniaFact]
	public void CellStates_NotEmpty()
	{
		var row = CreateRow();

		row.CellStates.Count.Should().BeGreaterThan(0);
	}

	[AvaloniaFact]
	public void CellStates_ActionColumn_IsEnabled()
	{
		var row = CreateRow(RecipeTestDriver.PauseActionId);

		row.CellStates["action"].Should().Be(CellState.Enabled);
	}

	[AvaloniaFact]
	public void GroupItemsByColumn_ExposesItems_ForGroupBoundColumn()
	{
		var row = CreateRow(RecipeTestDriver.WithGroupActionId);

		row.GroupItemsByColumn.Should().ContainKey(RecipeTestDriver.TargetColumn);
		var items = row.GroupItemsByColumn[RecipeTestDriver.TargetColumn];
		items.Should().NotBeEmpty();
		items.Select(item => item.Id).Should().BeInAscendingOrder();
	}

	[AvaloniaFact]
	public void GroupItemsByColumn_OmitsKey_ForNonGroupBoundColumn()
	{
		// Pre-population is intentionally scoped to ActionTargetComboBox columns so non-group
		// columns (text/property/action) do not accumulate empty-list entries.
		var row = CreateRow(RecipeTestDriver.WithGroupActionId);

		row.GroupItemsByColumn.Should().NotContainKey(RecipeTestDriver.StepDurationColumn);
		row.GroupItemsByColumn.Should().NotContainKey(RecipeTestDriver.CommentColumn);
	}

	[AvaloniaFact]
	public void GroupItemsByColumn_PrepopulatesEmptyList_ForGroupColumnAbsentFromActionProperties()
	{
		// Wait action has no `target` property, but `target` is a group-bound column in the registry.
		// The dictionary pre-populates such keys so bindings for cross-action grids never hit
		// a missing key (which would yield UnsetValue and bypass the converter contract).
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		row.GroupItemsByColumn.Should().ContainKey(RecipeTestDriver.TargetColumn);
		row.GroupItemsByColumn[RecipeTestDriver.TargetColumn].Should().BeEmpty();
	}

	[AvaloniaFact]
	public void GroupItemsByColumn_OmitsKey_WhenActionGroupResolutionFails()
	{
		// When an action property references a group that does not exist in the metadata registry,
		// the view-model skips it rather than surfacing the property key with an empty list. The
		// property key is also not a registered group-bound column, so the pre-population loop
		// does not insert it either — the key is fully absent from the dictionary.
		var unresolvedGroupProperty = new ActionPropertyDefinition(
			Key: "phantom_column",
			GroupName: "nonexistent_group",
			PropertyTypeId: "enum",
			DefaultValue: null);
		var actionWithUnresolvedGroup = new ActionDefinition(
			Id: 999,
			UiName: "Phantom",
			DeployDuration: DeployDuration.Immediate,
			Properties: new[] { unresolvedGroupProperty });
		var step = new Step(999, ImmutableDictionary<PropertyId, PropertyValue>.Empty);
		var cellStates = new Dictionary<string, CellState>();

		var row = new RecipeRowViewModel(1, step, actionWithUnresolvedGroup, _recipeMetadataRegistry, cellStates);

		row.GroupItemsByColumn.Should().NotContainKey("phantom_column");
	}

	public static TheoryData<string, string> ColumnUnitsData => new()
	{
		{ "step_start_time", "с" },
		{ RecipeTestDriver.StepDurationColumn, "s" },
		{ RecipeTestDriver.CommentColumn, "" },
	};

	[AvaloniaTheory]
	[MemberData(nameof(ColumnUnitsData))]
	public void ColumnUnits_ExposesExpectedUnitsForKnownColumns(string columnKey, string expectedUnits)
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		row.ColumnUnits.Should().ContainKey(columnKey)
			.WhoseValue.Should().Be(expectedUnits);
	}

	[AvaloniaTheory]
	[InlineData("step_start_time", "time_hms")]
	[InlineData(RecipeTestDriver.StepDurationColumn, "time_hms")]
	public void ColumnFormatKinds_ExposesExpectedFormatKindForKnownColumns(string columnKey, string expectedFormatKind)
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		row.ColumnFormatKinds.Should().ContainKey(columnKey)
			.WhoseValue.Should().Be(expectedFormatKind);
	}

	[AvaloniaFact]
	public void Dispose_NullsEventDelegates()
	{
		var row = CreateRow();
		var handlerCalled = false;
		row.PropertyValueChanged += (_, _) => handlerCalled = true;

		row.Dispose();
		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "5");

		handlerCalled.Should().BeFalse();
	}
}
