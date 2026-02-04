using Domain.State;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Timings;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Timings")]
public sealed class CoreTimingTests
{
	[Fact]
	public async Task EmptyRecipe_ZeroDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// NewRecipe already called by DomainFacade.Initialize
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task SingleWaitStep_TotalDurationMatchesStepDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		const float Duration = 15f;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(Duration);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(Duration));
	}

	[Fact]
	public async Task MultipleWaitSteps_TotalDurationIsCumulative()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(20f).AddWait(30f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public async Task StepStartTimes_AccumulateCorrectly()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(20f).AddWait(30f);

		var startTimes = driver.Snapshot.StepStartTimes;

		startTimes[0].Should().Be(TimeSpan.Zero);
		startTimes[1].Should().Be(TimeSpan.FromSeconds(10));
		startTimes[2].Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public async Task ImmediateAction_ZeroDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddPause();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task MixedActions_OnlyLongLastingContributeToTotalDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Pause (immediate) + Wait (longlasting) + ForLoop (immediate)
		driver.AddPause().AddWait(15f).AddFor(3);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public async Task UpdateDuration_RecalculatesTotal()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(10f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(20));

		driver.SetDuration(0, 30f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(40));
	}

	[Fact]
	public async Task RemoveStep_RecalculatesTotalDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(20f).AddWait(30f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(60));

		driver.RemoveStep(1);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(40));
	}
}
