using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Plc;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

/// <summary>
/// Exercises the transposed surface's selector-edit path end to end on the nested-action config
/// (action 300 "Branching" with a <c>branch_sel</c> selector whose value 1 pulls in the
/// <c>sub_value</c> column): one mutation applies the edit, recomputes applicability, and marks
/// the changed cells through <c>RecipeRowUpdateSynchronizer</c>, the same path as canonical.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedSelectorEditTests : IAsyncLifetime
{
	private const int BranchingActionId = 300;
	private const string SelectorColumn = "branch_sel";
	private const string SubColumn = "sub_value";
	private const string AutoValue = "0";
	private const string ManualValue = "1";
	private const float SubDefaultFloat = 50f;

	private readonly ChangeMarkClearBroadcaster _changeMarkClearBroadcaster = new();

	private IServiceProvider _services = null!;
	private MessagePanelViewModel _messagePanel = null!;
	private RecipeCoordinator _coordinator = null!;
	private RecipeMetadataRegistry _registry = null!;
	private ComboBoxItemsProvider _comboBoxItemsProvider = null!;
	private TransposedRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync("Standalone/NestedActionsValid");
		_services = services;
		var registry = services.GetRequiredService<RecipeMetadataRegistry>();
		_registry = registry;
		_comboBoxItemsProvider = new ComboBoxItemsProvider(registry);
		_messagePanel = new MessagePanelViewModel();

		_coordinator = new RecipeCoordinator(
			session,
			services.GetRequiredService<PlcLifecycleManager>(),
			services.GetRequiredService<CsvService>(),
			services.GetRequiredService<SemiStep.Core.Configuration.AppConfiguration>(),
			registry,
			_messagePanel,
			NullLogger<RecipeCoordinator>.Instance);
		_coordinator.Initialize();

		_surface = new TransposedRecipeGridSurface(
			_coordinator,
			registry,
			_comboBoxItemsProvider,
			_services.GetRequiredService<SemiStep.Core.Configuration.AppConfiguration>().GridStyle,
			_messagePanel,
			_changeMarkClearBroadcaster,
			NullLogger<TransposedRecipeGridSurface>.Instance);
		_surface.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		_surface.Dispose();
		_coordinator.Dispose();
		_messagePanel.Dispose();
		return ValueTask.CompletedTask;
	}

	[AvaloniaFact]
	public void SelectorEditThroughCell_SeedsSubValue_MarksItChanged_AndMakesItApplicable()
	{
		AppendBranchingStep();
		var subCell = GetCell(SubColumn);
		subCell.IsApplicable.Should().BeFalse();
		var changedProperties = new List<string?>();
		subCell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

		GetCell(SelectorColumn).Value = ManualValue;

		subCell.Value.Should().Be(SubDefaultFloat);
		subCell.IsApplicable.Should().BeTrue();
		subCell.IsChanged.Should().BeTrue();
		changedProperties.Should().Contain(
			nameof(ParameterCellViewModel.IsApplicable),
			"a real applicability flip must notify the bound cell");
		GetCell(SelectorColumn).IsChanged.Should().BeFalse();
	}

	[AvaloniaFact]
	public void SelectorSwitchBack_DropsSeededColumn_FromValueApplicabilityAndChangedSet()
	{
		AppendBranchingStep();
		GetCell(SelectorColumn).Value = ManualValue;
		GetCell(SubColumn).IsChanged.Should().BeTrue();

		GetCell(SelectorColumn).Value = AutoValue;

		var subCell = GetCell(SubColumn);
		subCell.Value.Should().BeNull();
		subCell.IsApplicable.Should().BeFalse();
		subCell.IsChanged.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ReadOnlySurface_BlocksSelectorEdit()
	{
		AppendBranchingStep();
		GetCell(SelectorColumn).Value = ManualValue;
		var stubS7 = _services.GetRequiredService<StubS7Service>();
		stubS7.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = true });
		_surface.IsReadOnly.Should().BeTrue();

		GetCell(SelectorColumn).Value = AutoValue;

		GetCell(SubColumn).Value.Should().Be(SubDefaultFloat);
		GetCell(SubColumn).IsApplicable.Should().BeTrue();
	}

	[AvaloniaFact]
	public void SelectorEditOnCanonicalSibling_SyncsApplicabilityAndChanged_OnTransposedColumn()
	{
		var gridStyle = _services
			.GetRequiredService<SemiStep.Core.Configuration.AppConfiguration>().GridStyle;
		using var canonicalSurface = new CanonicalRecipeGridSurface(
			_coordinator,
			_registry,
			_comboBoxItemsProvider,
			new ColumnBuilder(gridStyle, _registry, _comboBoxItemsProvider),
			_messagePanel,
			_changeMarkClearBroadcaster,
			NullLogger<CanonicalRecipeGridSurface>.Instance);
		canonicalSurface.Initialize();
		AppendBranchingStep();

		var subCell = GetCell(SubColumn);
		subCell.IsApplicable.Should().BeFalse();

		canonicalSurface.RecipeRows[0].SetPropertyValue(SelectorColumn, ManualValue);

		subCell.Value.Should().Be(SubDefaultFloat);
		subCell.IsApplicable.Should().BeTrue(
			"the transposed sibling's applicability must follow a selector edit made on canonical");
		subCell.IsChanged.Should().BeTrue(
			"the transposed sibling must mark the seeded column changed");

		GetCell(SelectorColumn).Value = AutoValue;

		canonicalSurface.RecipeRows[0].IsApplicable(SubColumn).Should().BeFalse(
			"the canonical sibling's applicability must follow a selector edit made on transposed");
		canonicalSurface.RecipeRows[0].IsChanged(SubColumn).Should().BeFalse();
	}

	private void AppendBranchingStep()
	{
		_coordinator.AppendStep(BranchingActionId);
	}

	private ParameterCellViewModel GetCell(string parameterKey)
	{
		return _surface.StepColumns.Single().Cells
			.Single(cell => cell.Descriptor.ParameterKey == parameterKey);
	}
}
