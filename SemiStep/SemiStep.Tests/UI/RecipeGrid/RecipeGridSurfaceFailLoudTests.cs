using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// Pins the fail-loud creation contract: an unknown action key during projection throws instead
// of silently skipping the step (which would desync index-based dispatch). The surfaces are
// constructed directly with a registry lacking the seeded action key — the UIFixture factories
// share the fixture registry and cannot produce the mismatch. On the Initialize() path the
// throw escapes and crashes; on the mutation path the surface's OnMutation catches it, reports
// to the message panel, and lets the multicast Mutated invocation proceed, so a probe handler
// subscribed after the failing surface still receives the signal.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeGridSurfaceFailLoudTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
	}

	public async ValueTask DisposeAsync()
	{
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void CanonicalInitialize_UnknownActionKey_ThrowsWithStepNumberAndKey()
	{
		using var surface = new CanonicalRecipeGridSurface(
			_fixture.Coordinator,
			BuildRegistryWithoutWaitAction(),
			_fixture.ColumnBuilder,
			_fixture.MessagePanel,
			_fixture.ClickAwayBroadcaster,
			NullLogger<CanonicalRecipeGridSurface>.Instance);

		var act = () => surface.Initialize();

		act.Should().Throw<InvalidOperationException>()
			.WithMessage($"*Step 1*unknown action key*{RecipeTestDriver.WaitActionId}*");
	}

	[AvaloniaFact]
	public void TransposedInitialize_UnknownActionKey_ThrowsWithStepNumberAndKey()
	{
		using var surface = new TransposedRecipeGridSurface(
			_fixture.Coordinator,
			BuildRegistryWithoutWaitAction(),
			_fixture.AppConfiguration.GridStyle,
			_fixture.MessagePanel,
			_fixture.ClickAwayBroadcaster,
			NullLogger<TransposedRecipeGridSurface>.Instance);

		var act = () => surface.Initialize();

		act.Should().Throw<InvalidOperationException>()
			.WithMessage($"*Step 1*unknown action key*{RecipeTestDriver.WaitActionId}*");
	}

	[AvaloniaFact]
	public void CanonicalMutation_UnknownActionKey_ReportsErrorAndKeepsSiblingSubscribersAlive()
	{
		using var surface = new CanonicalRecipeGridSurface(
			_fixture.Coordinator,
			BuildRegistryWithoutWaitAction(),
			_fixture.ColumnBuilder,
			_fixture.MessagePanel,
			_fixture.ClickAwayBroadcaster,
			NullLogger<CanonicalRecipeGridSurface>.Instance);
		var probeSignals = new List<MutationSignal>();
		_fixture.Coordinator.Mutated += signal => probeSignals.Add(signal);

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		probeSignals.Should().ContainSingle(signal => signal is MutationSignal.StepAppended,
			"a projection failure in one surface must not starve later-subscribed Mutated handlers");
		surface.RecipeRows.Should().BeEmpty("the failed projection is left as-is");
		var errorEntry = _fixture.MessagePanel.Entries.Should()
			.ContainSingle(entry => entry.IsError).Subject;
		errorEntry.Message.Should().Match($"*Step 2*unknown action key*{RecipeTestDriver.WaitActionId}*");
	}

	[AvaloniaFact]
	public void TransposedMutation_UnknownActionKey_ReportsErrorAndKeepsSiblingSubscribersAlive()
	{
		using var surface = new TransposedRecipeGridSurface(
			_fixture.Coordinator,
			BuildRegistryWithoutWaitAction(),
			_fixture.AppConfiguration.GridStyle,
			_fixture.MessagePanel,
			_fixture.ClickAwayBroadcaster,
			NullLogger<TransposedRecipeGridSurface>.Instance);
		var probeSignals = new List<MutationSignal>();
		_fixture.Coordinator.Mutated += signal => probeSignals.Add(signal);

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		probeSignals.Should().ContainSingle(signal => signal is MutationSignal.StepAppended,
			"a projection failure in one surface must not starve later-subscribed Mutated handlers");
		surface.StepColumns.Should().BeEmpty("the failed projection is left as-is");
		var errorEntry = _fixture.MessagePanel.Entries.Should()
			.ContainSingle(entry => entry.IsError).Subject;
		errorEntry.Message.Should().Match($"*Step 2*unknown action key*{RecipeTestDriver.WaitActionId}*");
	}

	private RecipeMetadataRegistry BuildRegistryWithoutWaitAction()
	{
		var actions = _fixture.AppConfiguration.Actions
			.Where(pair => pair.Key != RecipeTestDriver.WaitActionId)
			.ToDictionary(pair => pair.Key, pair => pair.Value);

		return new RecipeMetadataRegistry(_fixture.AppConfiguration with { Actions = actions });
	}
}
