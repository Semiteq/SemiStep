using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Snapshot;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Snapshot")]
public sealed class CoreSnapshotStateTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void RejectedMutation_LeavesRecipeAndValidStateUnchanged()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddWait(5f).AddWait(10f);
		driver.AddFor(1).AddFor(1).AddFor(1).AddFor(1).AddWait(1f);

		driver.IsValid.Should().BeFalse("unclosed For loops block validity");

		var stepCountBeforeRejection = fixture.Session.Current.StepCount;
		var lastValidStepCountBeforeRejection = fixture.Session.LastValidRecipe.StepCount;

		var result = fixture.Session.AppendStep(RecipeTestDriver.EndForLoopActionId);

		result.IsFailed.Should().BeTrue("closing a 4th nested loop exceeds the maximum nesting depth");
		driver.IsValid.Should().BeFalse("mutation was rejected, state still has unclosed For loops");
		fixture.Session.Current.StepCount.Should().Be(stepCountBeforeRejection, "rejected mutation must not change the recipe");
		fixture.Session.LastValidRecipe.StepCount.Should().Be(lastValidStepCountBeforeRejection, "last valid recipe must not change when mutation is rejected");
	}

	[Fact]
	public void LastValidRecipe_UpdatesAfterFix()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(3).AddWait(1f);
		driver.IsValid.Should().BeFalse("unclosed For blocks validity");

		driver.AddEndFor();
		driver.IsValid.Should().BeTrue("adding EndFor closes the loop");

		fixture.Session.LastValidRecipe.StepCount.Should().Be(3);
	}
}
