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
	private RecipeSession _session = null!;
	private RecipeMetadataRegistry _recipeMetadataRegistry = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync("WithGroups");
		_session = session;
		_recipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		_session.Reset();
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}

	private RecipeRowViewModel CreateRow(int actionId = RecipeTestDriver.WaitActionId)
	{
		_session.AppendStep(actionId);
		var step = _session.Current.Steps[0];
		var action = _recipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var inapplicableColumns = BuildInapplicableColumns(action);
		return new RecipeRowViewModel(1, step, action, _recipeMetadataRegistry, inapplicableColumns);
	}

	private IReadOnlySet<string> BuildInapplicableColumns(ActionDefinition action)
	{
		var inapplicable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var col in _recipeMetadataRegistry.GetAllColumns())
		{
			if (CellStateResolver.IsInapplicable(col, action))
			{
				inapplicable.Add(col.Key);
			}
		}
		return inapplicable;
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
	public void SetPropertyValue_ActionWithSameId_DoesNotFireEvent()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var eventFired = false;
		row.ActionChanged += _ => eventFired = true;

		row.SetPropertyValue("action", RecipeTestDriver.WaitActionId.ToString());

		eventFired.Should().BeFalse();
	}

	[AvaloniaFact]
	public void SetPropertyValue_ActionWithDifferentId_FiresEvent()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		row.SetPropertyValue("action", RecipeTestDriver.ForLoopActionId.ToString());

		receivedActionId.Should().Be(RecipeTestDriver.ForLoopActionId);
	}

	[AvaloniaFact]
	public void SetPropertyValue_PropertyWithSameValue_DoesNotFirePropertyChanged()
	{
		var row = CreateRow();
		_session.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");
		row.UpdateStep(_session.Current.Steps[0]);
		var canonicalValue = row.GetPropertyValue(RecipeTestDriver.StepDurationColumn)?.ToString();

		var eventFired = false;
		row.PropertyValueChanged += (_, _) => eventFired = true;

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, canonicalValue);

		eventFired.Should().BeFalse();
	}

	[AvaloniaFact]
	public void SetPropertyValue_PropertyWithDifferentValue_FiresPropertyChanged()
	{
		var row = CreateRow();
		_session.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "5");
		row.UpdateStep(_session.Current.Steps[0]);

		var receivedColumnKey = string.Empty;
		var receivedValue = string.Empty;
		row.PropertyValueChanged += (key, value) =>
		{
			receivedColumnKey = key;
			receivedValue = value ?? string.Empty;
		};

		row.SetPropertyValue(RecipeTestDriver.StepDurationColumn, "7");

		receivedColumnKey.Should().Be(RecipeTestDriver.StepDurationColumn);
		receivedValue.Should().Be("7");
	}

	[AvaloniaFact]
	public void UpdateStep_RaisesItemArrayPropertyChanged()
	{
		var row = CreateRow();
		var changedProperties = new List<string>();
		row.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? "");

		var updatedStep = _session.Current.Steps[0];
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
	public void InapplicableColumns_ActionColumn_IsApplicable()
	{
		var row = CreateRow(RecipeTestDriver.PauseActionId);

		row.IsApplicable("action").Should().BeTrue();
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
	public void GroupItemsByColumn_PrepopulatesEmptyList_ForActionWithoutGroupProperty()
	{
		// Wait action has no `target` property, but `target` is a registered ActionTargetComboBox
		// column. The dict pre-populates with an empty list so that, when a group cell is recycled
		// onto this row, ItemsSourceBinding resolves cleanly without KeyNotFoundException.
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		row.GroupItemsByColumn.Should().ContainKey(RecipeTestDriver.TargetColumn);
		row.GroupItemsByColumn[RecipeTestDriver.TargetColumn].Should().BeEmpty();
	}

	[AvaloniaFact]
	public void GroupItemsByColumn_ReturnsEmptyList_WhenActionGroupResolutionFails()
	{
		// When an action property references a group that does not exist in the metadata registry,
		// RecipeMetadataRegistry.GetComboBoxItems returns Array.Empty<>(). The row VM stores this
		// reference so the key is present with an empty list — binding never sees UnsetValue.
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
		var inapplicableColumns = new HashSet<string>();

		var row = new RecipeRowViewModel(1, step, actionWithUnresolvedGroup, _recipeMetadataRegistry, inapplicableColumns);

		row.GroupItemsByColumn.Should().ContainKey("phantom_column");
		row.GroupItemsByColumn["phantom_column"].Should().BeEmpty();
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
