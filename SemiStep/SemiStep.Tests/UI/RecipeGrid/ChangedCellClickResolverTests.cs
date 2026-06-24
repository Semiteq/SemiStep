using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class ChangedCellClickResolverTests : IAsyncLifetime
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

	[AvaloniaFact]
	public void NoPending_PressChangedCell_ArmsPending_NoClear()
	{
		var row = CreateRow(0);
		row.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(null, row, "colX");

		cellToClear.Should().BeNull();
		newPending.Should().Be((row, "colX"));
	}

	[AvaloniaFact]
	public void NoPending_PressUnchangedCell_NoArm_NoClear()
	{
		var row = CreateRow(0);
		row.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(null, row, "colY");

		cellToClear.Should().BeNull();
		newPending.Should().BeNull();
	}

	[AvaloniaFact]
	public void NoPending_PressHeader_NoArm_NoClear()
	{
		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(null, null, null);

		cellToClear.Should().BeNull();
		newPending.Should().BeNull();
	}

	[AvaloniaFact]
	public void Pending_PressSameCell_KeepsArmed_NoClear()
	{
		var row = CreateRow(0);
		row.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((row, "colX"), row, "colX");

		cellToClear.Should().BeNull();
		newPending.Should().Be((row, "colX"));
	}

	[AvaloniaFact]
	public void Pending_PressDifferentChangedCell_ClearsPending_RearmsToPressed()
	{
		var rowA = CreateRow(0);
		var rowB = CreateRow(1);
		rowA.MarkChanged(["colX"]);
		rowB.MarkChanged(["colZ"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((rowA, "colX"), rowB, "colZ");

		cellToClear.Should().Be((rowA, "colX"));
		newPending.Should().Be((rowB, "colZ"));
	}

	[AvaloniaFact]
	public void Pending_PressDifferentUnchangedCell_ClearsPending_NoRearm()
	{
		var rowA = CreateRow(0);
		var rowB = CreateRow(1);
		rowA.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((rowA, "colX"), rowB, "colY");

		cellToClear.Should().Be((rowA, "colX"));
		newPending.Should().BeNull();
	}

	[AvaloniaFact]
	public void Pending_PressHeader_ClearsPending_NoRearm()
	{
		var row = CreateRow(0);
		row.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((row, "colX"), null, null);

		cellToClear.Should().Be((row, "colX"));
		newPending.Should().BeNull();
	}

	[AvaloniaFact]
	public void Pending_PressDifferentColumnSameRow_ClearsPending()
	{
		var row = CreateRow(0);
		row.MarkChanged(["colX"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((row, "colX"), row, "colY");

		cellToClear.Should().Be((row, "colX"));
		newPending.Should().BeNull();
	}

	[AvaloniaFact]
	public void Pending_PressSameCellCaseInsensitive_KeepsArmed_NoClear()
	{
		var row = CreateRow(0);
		row.MarkChanged(["Temperature"]);

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve((row, "Temperature"), row, "temperature");

		cellToClear.Should().BeNull();
		newPending.Should().Be((row, "temperature"));
	}

	private RecipeRowViewModel CreateRow(int stepIndex, int actionId = RecipeTestDriver.WaitActionId)
	{
		_session.AppendStep(actionId);
		var step = _session.Current.Steps[stepIndex];
		var action = _recipeMetadataRegistry.GetAction(step.ActionKey).Value;
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(action, step, _recipeMetadataRegistry);
		return new RecipeRowViewModel(stepIndex + 1, step, action, _recipeMetadataRegistry, inapplicableColumns);
	}
}
