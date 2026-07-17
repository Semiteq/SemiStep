using System.Threading.Tasks;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Xunit;

namespace SemiStep.Tests.Performance.Harness;

[Trait("Component", "UI")]
[Trait("Area", "Performance")]
[Trait("Category", "Integration")]
public sealed class TransposedGridDriverTests
{
	[AvaloniaFact]
	public async Task ScrollToColumn_ChangesRealizedRange()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.ScrollToColumnChangesRealizedRange(driver);
	}

	[AvaloniaFact]
	public async Task AddSteps_IncreasesColumnCount()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.AddStepsIncreasesItemCount(driver);
	}

	[AvaloniaFact]
	public async Task RemoveSteps_DecreasesColumnCount()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.RemoveStepsDecreasesItemCount(driver);
	}

	[AvaloniaFact]
	public async Task SelectRange_IsReflectedInSelectionModel()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.SelectRangeIsReflectedInSelectionModel(driver);
	}

	[AvaloniaFact]
	public async Task WaitForIdle_DrainsDispatcherJobs()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.WaitForIdleDrainsDispatcherJobs(driver);
	}

	[AvaloniaFact]
	public async Task SnapshotScope_IsItemsPanelSubtree_NotWholeRoot()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.SnapshotScopeIsItemsPanelSubtreeNotWholeRoot(driver);
	}

	[AvaloniaFact]
	public async Task RealizedContainers_ReflectViewport()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.RealizedContainersReflectViewport(driver);
	}

	// Selection is the index-based accessor the selection-cost gate toggles inside its stopwatch window;
	// smoke-cover that a deselect/select through it is reflected in the surface selection.
	[AvaloniaFact]
	public async Task Selection_ToggleReflectedInSelectionModel()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);

		await driver.SelectRangeAsync(2, 6);
		driver.SelectedIndices.Should().Contain(4);

		driver.Selection.Deselect(4);
		await driver.WaitForIdleAsync();
		driver.SelectedIndices.Should().NotContain(4);

		driver.Selection.Select(4);
		await driver.WaitForIdleAsync();
		driver.SelectedIndices.Should().Contain(4);
	}
}
