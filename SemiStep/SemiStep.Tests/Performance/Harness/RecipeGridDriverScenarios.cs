using System.Linq;
using System.Threading.Tasks;

using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

namespace SemiStep.Tests.Performance.Harness;

// Parity scenario bodies: every assertion is expressed against IRecipeGridDriver only, so the exact
// same body runs against the transposed and the canonical driver. The two smoke-test classes are thin
// wrappers that build their driver and call in here, guaranteeing both orientations stay symmetric.
internal static class RecipeGridDriverScenarios
{
	public const int SeededStepCount = 60;

	public static async Task ScrollToColumnChangesRealizedRange(IRecipeGridDriver driver)
	{
		var before = driver.RealizedIndices;
		before.Should().NotBeEmpty("the seeded grid must realize a viewport of containers");

		await driver.ScrollToColumnAsync(SeededStepCount - 1);

		var after = driver.RealizedIndices;
		after.Should().NotBeEmpty();
		after.Should().NotEqual(
			before,
			"scrolling to the far end must realize a different range of containers");
		after.Max().Should().BeGreaterThan(
			before.Max(),
			"the far target index must become realized after the scroll");
	}

	public static async Task RealizedContainersReflectViewport(IRecipeGridDriver driver)
	{
		await driver.WaitForIdleAsync();

		var containers = driver.RealizedContainers;
		containers.Should().NotBeEmpty("a seeded grid must realize container controls for the survivor probe");
		containers.Count.Should().BeGreaterThanOrEqualTo(
			driver.RealizedIndices.Count,
			"every realized index maps to a realized container, so containers cannot be fewer than indices");
	}

	public static async Task AddStepsIncreasesItemCount(IRecipeGridDriver driver)
	{
		var before = driver.ItemCount;

		await driver.AddStepsAsync(5);

		driver.ItemCount.Should().Be(before + 5, "appending steps must grow the projected item count");
	}

	public static async Task RemoveStepsDecreasesItemCount(IRecipeGridDriver driver)
	{
		var before = driver.ItemCount;

		await driver.RemoveStepsAsync(5);

		driver.ItemCount.Should().Be(before - 5, "removing steps must shrink the projected item count");
	}

	public static async Task SelectRangeIsReflectedInSelectionModel(IRecipeGridDriver driver)
	{
		await driver.SelectRangeAsync(2, 6);

		driver.SelectedIndices.Should().Equal(
			2, 3, 4, 5, 6);
	}

	public static async Task WaitForIdleDrainsDispatcherJobs(IRecipeGridDriver driver)
	{
		var executed = false;
		Dispatcher.UIThread.Post(() => executed = true);

		executed.Should().BeFalse("the posted job must stay queued until the dispatcher is drained");

		await driver.WaitForIdleAsync();

		executed.Should().BeTrue("WaitForIdleAsync must drain queued dispatcher jobs");
	}

	public static Task SnapshotScopeIsItemsPanelSubtreeNotWholeRoot(IRecipeGridDriver driver)
	{
		driver.SnapshotScope.Should().NotBeSameAs((object)driver.Root);

		var scopeDescendants = driver.SnapshotScope.GetVisualDescendants().Count();
		var rootDescendants = driver.Root.GetVisualDescendants().Count();

		rootDescendants.Should().BeGreaterThan(
			scopeDescendants,
			"the snapshot scope is the items-panel subtree, a strict subset of the window");
		scopeDescendants.Should().BeGreaterThan(
			0,
			"the items-panel subtree must contain the realized containers");

		return Task.CompletedTask;
	}
}
