using System.Globalization;

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "RecipeRow")]
[Trait("Category", "Unit")]
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
		var inapplicableColumns = BuildInapplicableColumns(action, step);
		return new RecipeRowViewModel(1, step, action, _recipeMetadataRegistry, inapplicableColumns);
	}

	private IReadOnlySet<string> BuildInapplicableColumns(ActionDefinition action, Step step)
	{
		var activeColumnKeys = ActiveColumnSetResolver.Resolve(action, step);
		var inapplicable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in _recipeMetadataRegistry.GetAllColumns())
		{
			if (CellStateResolver.IsInapplicable(column, activeColumnKeys))
			{
				inapplicable.Add(column.Key);
			}
		}
		return inapplicable;
	}

	[AvaloniaFact]
	public void ActionMetadata_TwoRowsSameAction_ShareCachedDictionaries()
	{
		_session.AppendStep(RecipeTestDriver.WaitActionId);
		var step = _session.Current.Steps[0];
		var action = _recipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var inapplicable = BuildInapplicableColumns(action, step);

		var first = new RecipeRowViewModel(1, step, action, _recipeMetadataRegistry, inapplicable);
		var second = new RecipeRowViewModel(2, step, action, _recipeMetadataRegistry, inapplicable);

		second.ColumnUnits.Should().BeSameAs(first.ColumnUnits);
		second.ColumnFormatKinds.Should().BeSameAs(first.ColumnFormatKinds);
		second.GroupItemsByColumn.Should().BeSameAs(first.GroupItemsByColumn);
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
	public void SetPropertyValue_Action_FiresActionChangedEvent()
	{
		var row = CreateRow();
		var receivedActionId = -1;
		row.ActionChanged += id => receivedActionId = id;

		row.SetPropertyValue("action", RecipeTestDriver.ForLoopActionId.ToString(CultureInfo.InvariantCulture));

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

		row.SetPropertyValue("action", RecipeTestDriver.WaitActionId.ToString(CultureInfo.InvariantCulture));

		eventFired.Should().BeFalse();
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
	public void UpdateStep_RefreshesIndexerBinding()
	{
		var row = CreateRow();
		var textBlock = new TextBlock { DataContext = row };
		textBlock.Bind(TextBlock.TextProperty, new Binding($"[{RecipeTestDriver.CommentColumn}]"));

		const string NewComment = "updated";
		_session.UpdateStepProperty(0, RecipeTestDriver.CommentColumn, NewComment);
		row.UpdateStep(_session.Current.Steps[0]);

		textBlock.Text.Should().Be(NewComment);
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

	[AvaloniaTheory]
	[InlineData(RecipeTestDriver.StepDurationColumn)]
	[InlineData(RecipeTestDriver.CommentColumn)]
	public void ColumnUnits_MatchesConfiguredPropertyUnits(string columnKey)
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);

		row.ColumnUnits.Should().ContainKey(columnKey)
			.WhoseValue.Should().Be(ConfiguredUnitsOfColumn(columnKey));
	}

	[AvaloniaFact]
	public void ColumnUnits_StartTimeColumn_EqualsDurationPropertyUnits()
	{
		var row = CreateRow(RecipeTestDriver.WaitActionId);
		var durationUnits = ConfiguredUnitsOfColumn(RecipeTestDriver.StepDurationColumn);

		durationUnits.Should().NotBeNullOrEmpty();
		row.ColumnUnits.Should().ContainKey("step_start_time")
			.WhoseValue.Should().Be(durationUnits);
	}

	[Fact]
	public void StartTimeUnits_DurationPropertyWithEmptyUnits_AreEmpty()
	{
		var duration = TestPropertyTypeDefinitionBuilder.CreateFloat("time") with { Units = "" };
		var durationColumn = new GridColumnDefinition(
			Key: StepValueParser.StepDurationColumnKey,
			ColumnType: ColumnTypes.PropertyField,
			UiName: "Duration",
			PropertyTypeId: "time",
			ReadOnly: false,
			SaveToCsv: true);
		var registry = TestRecipeMetadataRegistryFactory.Build(
			[duration, .. TestRecipeMetadataRegistryFactory.DefaultStringProperty().Values],
			columns: new Dictionary<string, GridColumnDefinition> { [durationColumn.Key] = durationColumn });

		TimeFormatHelper.ResolveStartTimeUnits(registry).Should().BeEmpty();
	}

	[Fact]
	public void StartTimeUnits_NoDurationColumn_AreNull()
	{
		var registry = TestRecipeMetadataRegistryFactory.Build(
			TestRecipeMetadataRegistryFactory.DefaultStringProperty().Values);

		TimeFormatHelper.ResolveStartTimeUnits(registry).Should().BeNull();
	}

	private string? ConfiguredUnitsOfColumn(string columnKey)
	{
		var column = _recipeMetadataRegistry.GetColumn(columnKey).Value;

		return _recipeMetadataRegistry.GetProperty(column.PropertyTypeId).Value.Units;
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
	public void ChangedColumns_InitiallyEmpty()
	{
		var row = CreateRow();

		row.ChangedColumns.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void MarkChanged_SetsMembership_AndAssignsNewInstance()
	{
		var row = CreateRow();
		var before = row.ChangedColumns;

		row.MarkChanged(["alpha", "beta"]);

		row.ChangedColumns.Should().NotBeSameAs(before);
		row.ChangedColumns.Should().BeEquivalentTo("alpha", "beta");
		row.IsChanged("ALPHA").Should().BeTrue();
	}

	[AvaloniaFact]
	public void ApplyChangedDelta_AddsAndRemoves()
	{
		var row = CreateRow();
		row.MarkChanged(["alpha", "beta"]);
		var before = row.ChangedColumns;

		row.ApplyChangedDelta(["gamma"], ["alpha"]);

		row.ChangedColumns.Should().NotBeSameAs(before);
		row.ChangedColumns.Should().BeEquivalentTo("beta", "gamma");
	}

	[AvaloniaFact]
	public void ClearChanged_RemovesOneKey_WhenPresent()
	{
		var row = CreateRow();
		row.MarkChanged(["alpha", "beta"]);
		var before = row.ChangedColumns;

		row.ClearChanged("alpha");

		row.ChangedColumns.Should().NotBeSameAs(before);
		row.ChangedColumns.Should().BeEquivalentTo("beta");
	}

	[AvaloniaFact]
	public void ClearChanged_AbsentKey_IsNoOp_AndKeepsInstance()
	{
		var row = CreateRow();
		row.MarkChanged(["alpha"]);
		var before = row.ChangedColumns;

		row.ClearChanged("missing");

		row.ChangedColumns.Should().BeSameAs(before);
		row.ChangedColumns.Should().BeEquivalentTo("alpha");
	}

	[AvaloniaFact]
	public void ClearAllChanged_EmptiesSet_WhenNonEmpty()
	{
		var row = CreateRow();
		row.MarkChanged(["alpha", "beta"]);
		var before = row.ChangedColumns;

		row.ClearAllChanged();

		row.ChangedColumns.Should().NotBeSameAs(before);
		row.ChangedColumns.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ClearAllChanged_AlreadyEmpty_IsNoOp_AndKeepsInstance()
	{
		var row = CreateRow();
		var before = row.ChangedColumns;

		row.ClearAllChanged();

		row.ChangedColumns.Should().BeSameAs(before);
	}

	[AvaloniaFact]
	public void IsChanged_ReflectsMembership()
	{
		var row = CreateRow();
		row.MarkChanged(["alpha"]);

		row.IsChanged("alpha").Should().BeTrue();
		row.IsChanged("beta").Should().BeFalse();
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
