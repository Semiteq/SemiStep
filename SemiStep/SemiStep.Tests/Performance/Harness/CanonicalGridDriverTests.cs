using System.Threading.Tasks;

using Avalonia.Headless.XUnit;

using Xunit;

namespace SemiStep.Tests.Performance.Harness;

[Trait("Component", "UI")]
[Trait("Area", "Performance")]
[Trait("Category", "Integration")]
public sealed class CanonicalGridDriverTests
{
	[AvaloniaFact]
	public async Task ScrollToColumn_ChangesRealizedRange()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.ScrollToColumnChangesRealizedRange(driver);
	}

	[AvaloniaFact]
	public async Task AddSteps_IncreasesRowCount()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.AddStepsIncreasesItemCount(driver);
	}

	[AvaloniaFact]
	public async Task RemoveSteps_DecreasesRowCount()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.RemoveStepsDecreasesItemCount(driver);
	}

	[AvaloniaFact]
	public async Task SelectRange_IsReflectedInSelectionModel()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.SelectRangeIsReflectedInSelectionModel(driver);
	}

	[AvaloniaFact]
	public async Task WaitForIdle_DrainsDispatcherJobs()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.WaitForIdleDrainsDispatcherJobs(driver);
	}

	[AvaloniaFact]
	public async Task SnapshotScope_IsItemsPanelSubtree_NotWholeRoot()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.SnapshotScopeIsItemsPanelSubtreeNotWholeRoot(driver);
	}

	[AvaloniaFact]
	public async Task RealizedContainers_ReflectViewport()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			stepCount: RecipeGridDriverScenarios.SeededStepCount);
		await RecipeGridDriverScenarios.RealizedContainersReflectViewport(driver);
	}
}
