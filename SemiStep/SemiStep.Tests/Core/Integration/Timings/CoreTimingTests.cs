using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Timings;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Timings")]
public sealed class CoreTimingTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void EmptyRecipe_ZeroDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void StepStartTimes_AccumulateCorrectly()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f).AddWait(20f).AddWait(30f);

		var startTimes = driver.Snapshot.StepStartTimes;

		startTimes[0].Should().Be(TimeSpan.Zero);
		startTimes[1].Should().Be(TimeSpan.FromSeconds(10));
		startTimes[2].Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ImmediateAction_ZeroDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddPause();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void MixedActions_OnlyLongLastingContributeToTotalDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddPause().AddWait(15f).AddFor(3);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void RemoveStep_RecalculatesTotalDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f).AddWait(20f).AddWait(30f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(60));

		driver.RemoveStep(1);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(40));
	}
}
