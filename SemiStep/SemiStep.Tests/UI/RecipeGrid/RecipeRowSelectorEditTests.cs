using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>
/// Exercises the selector-edit recompute end to end on the nested-action config (action 300
/// "Branching" with a <c>branch_sel</c> selector whose value 1 pulls in the <c>sub_value</c>
/// column). Maps onto the RIE example in the plan: <c>branch_sel</c> ~ <c>icp_match</c>,
/// value 0 "Auto" ~ Авто, value 1 "Manual" ~ Ручной, <c>sub_value</c> ~ <c>icp_load</c>.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "NestedActions")]
[Trait("Category", "Integration")]
public sealed class RecipeRowSelectorEditTests : IAsyncLifetime
{
	private const int BranchingActionId = 300;
	private const string SelectorColumn = "branch_sel";
	private const string SubColumn = "sub_value";
	private const string NonSelectorColumn = "comment";
	private const string AutoValue = "0";
	private const string ManualValue = "1";
	private const float SubDefaultFloat = 50f;

	private IServiceProvider _services = null!;
	private RecipeSession _session = null!;
	private RecipeMetadataRegistry _registry = null!;
	private MessagePanelViewModel _messagePanel = null!;
	private RecipeCoordinator _coordinator = null!;
	private CanonicalRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync("Standalone/NestedActionsValid");
		_services = services;
		_session = session;
		_registry = services.GetRequiredService<RecipeMetadataRegistry>();
		_messagePanel = new MessagePanelViewModel();

		_coordinator = new RecipeCoordinator(
			_session,
			services.GetRequiredService<PlcLifecycleManager>(),
			services.GetRequiredService<CsvService>(),
			services.GetRequiredService<ImportedRecipeValidator>(),
			services.GetRequiredService<SemiStep.Core.Configuration.AppConfiguration>(),
			_registry,
			_messagePanel,
			NullLogger<RecipeCoordinator>.Instance);
		_coordinator.Initialize();

		_surface = new CanonicalRecipeGridSurface(
			_coordinator,
			_registry,
			new ColumnBuilder(GridStyleOptions.Default, _registry),
			_messagePanel,
			NullLogger<CanonicalRecipeGridSurface>.Instance);
		_surface.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		_surface.Dispose();
		_coordinator.Dispose();
		_messagePanel.Dispose();
		return ValueTask.CompletedTask;
	}

	private RecipeRowViewModel AppendBranchingRow()
	{
		_coordinator.AppendStep(BranchingActionId);
		return _surface.RecipeRows.Single();
	}

	[AvaloniaFact]
	public void InitialAutoSelection_SubColumnInapplicable()
	{
		var row = AppendBranchingRow();

		row.InapplicableColumns.Should().Contain(SubColumn);
		row.IsApplicable(SubColumn).Should().BeFalse();
	}

	[AvaloniaFact]
	public void SwitchToManual_SeedsSubValueDefault_AndMakesItApplicable()
	{
		var row = AppendBranchingRow();

		row.SetPropertyValue(SelectorColumn, ManualValue);

		row.GetPropertyValue(SubColumn).Should().Be(SubDefaultFloat);
		row.InapplicableColumns.Should().NotContain(SubColumn);
		row.IsApplicable(SubColumn).Should().BeTrue();
	}

	[AvaloniaFact]
	public void SwitchBackToAuto_DropsSubValue_AndGreysIt()
	{
		var row = AppendBranchingRow();
		row.SetPropertyValue(SelectorColumn, ManualValue);
		row.GetPropertyValue(SubColumn).Should().Be(SubDefaultFloat);

		row.SetPropertyValue(SelectorColumn, AutoValue);

		row.GetPropertyValue(SubColumn).Should().BeNull();
		row.InapplicableColumns.Should().Contain(SubColumn);
		row.IsApplicable(SubColumn).Should().BeFalse();
	}

	[AvaloniaFact]
	public void SwitchToAutoThenUndo_RestoresManualSelectorAndDroppedValue_InOneStep()
	{
		var row = AppendBranchingRow();
		row.SetPropertyValue(SelectorColumn, ManualValue);
		row.SetPropertyValue(SubColumn, "73");
		row.GetPropertyValue(SubColumn).Should().Be(73f);

		// Switching back to Auto drops sub_value; a single Undo must restore BOTH the Manual
		// selector value and the prior sub_value in one step (the batched-mutation undo unit).
		row.SetPropertyValue(SelectorColumn, AutoValue);
		row.GetPropertyValue(SubColumn).Should().BeNull();

		_coordinator.Undo();

		var restored = _surface.RecipeRows.Single();
		restored.GetPropertyValue(SelectorColumn).Should().Be(1);
		restored.GetPropertyValue(SubColumn).Should().Be(73f);
	}

	[AvaloniaFact]
	public void OrdinaryEdit_DoesNotRecomputeApplicability_ReferenceUnchanged()
	{
		// An edit on a non-selector column must route through the plain PropertyUpdated path
		// (UpdateStepProperty -> UpdateSingleRowInPlace -> UpdateStep) and must NOT trigger an
		// applicability recompute: InapplicableColumns keeps the same instance and raises no
		// PropertyChanged for itself.
		var row = AppendBranchingRow();
		var before = row.InapplicableColumns;
		var changed = new List<string>();
		row.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

		row.SetPropertyValue(NonSelectorColumn, "hello");

		row.GetPropertyValue(NonSelectorColumn).Should().Be("hello");
		row.InapplicableColumns.Should().BeSameAs(before);
		changed.Should().NotContain(nameof(RecipeRowViewModel.InapplicableColumns));
	}

	[AvaloniaFact]
	public void SelectorReassignment_FlipsInapplicableColumns_ReferenceChanges()
	{
		var row = AppendBranchingRow();
		var changed = new List<string>();
		row.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
		var before = row.InapplicableColumns;

		row.SetPropertyValue(SelectorColumn, ManualValue);

		changed.Should().Contain(nameof(RecipeRowViewModel.InapplicableColumns));
		row.InapplicableColumns.Should().NotBeSameAs(before);
	}

	[AvaloniaFact]
	public void SwitchToManual_MarksSeededColumnChanged()
	{
		var row = AppendBranchingRow();

		row.SetPropertyValue(SelectorColumn, ManualValue);

		row.ChangedColumns.Should().Contain(SubColumn);
		row.ChangedColumns.Should().NotContain(SelectorColumn);
	}

	[AvaloniaFact]
	public void SwitchBackToAuto_DropsSeededColumnFromChanged()
	{
		var row = AppendBranchingRow();
		row.SetPropertyValue(SelectorColumn, ManualValue);
		row.ChangedColumns.Should().Contain(SubColumn);

		row.SetPropertyValue(SelectorColumn, AutoValue);

		row.ChangedColumns.Should().NotContain(SubColumn);
	}

	[AvaloniaFact]
	public void FailedSelectorEdit_LeavesChangedColumnsUntouched()
	{
		var row = AppendBranchingRow();
		row.SetPropertyValue(SelectorColumn, ManualValue);
		row.ChangedColumns.Should().Contain(SubColumn);
		var beforeFailedEdit = row.ChangedColumns;

		// "2" parses as an int (so TryBuildSelectorEdit builds a SelectorEdit and routes through the
		// selector path), but it is not a defined match_mode group value, so the coordinator rejects
		// it. OnSelectorValueChanged takes the result.IsFailed early-return before ApplyChangedDelta:
		// a rejected selector edit must not mutate the changed set.
		row.SetPropertyValue(SelectorColumn, "2");

		row.ChangedColumns.Should().BeSameAs(beforeFailedEdit);
		row.ChangedColumns.Should().Contain(SubColumn);
	}

	[AvaloniaFact]
	public void BlockedSelectorEdit_LeavesChangedColumnsUntouched()
	{
		var row = AppendBranchingRow();
		row.SetPropertyValue(SelectorColumn, ManualValue);
		row.ChangedColumns.Should().Contain(SubColumn);

		// Going recipe-active both locks editing AND clears all changed highlights (execution
		// start is one of the three clear triggers). Capture the post-clear set, then confirm the
		// blocked selector edit takes OnSelectorValueChanged's read-only early-return and does not
		// apply the changed delta (same instance, no new membership).
		var stubS7 = _services.GetRequiredService<StubS7Service>();
		stubS7.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = true });
		_surface.IsReadOnly.Should().BeTrue();
		var afterExecutionStart = row.ChangedColumns;

		row.SetPropertyValue(SelectorColumn, AutoValue);

		row.ChangedColumns.Should().BeSameAs(afterExecutionStart);
	}

	[AvaloniaFact]
	public void InapplicableBinding_Converter_FlipsOnReassignment()
	{
		// The cell theme is driven by the OneWay InapplicableColumns binding. Reproduce the
		// converter the binding uses and confirm it reports the new applicability after the
		// row reassigns InapplicableColumns on the selector edit.
		var row = AppendBranchingRow();
		var converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
			set => set is not null && set.Contains(SubColumn));

		var inapplicableBefore = converter.Convert(row.InapplicableColumns, typeof(bool), null, CultureInfo.InvariantCulture);
		inapplicableBefore.Should().Be(true);

		row.SetPropertyValue(SelectorColumn, ManualValue);

		var inapplicableAfter = converter.Convert(row.InapplicableColumns, typeof(bool), null, CultureInfo.InvariantCulture);
		inapplicableAfter.Should().Be(false);
	}
}
