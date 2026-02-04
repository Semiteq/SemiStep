using Domain.State;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Mutation;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Mutation")]
public sealed class CoreMutationTests
{
	private const float DefaultWaitDurationSeconds = 10f;

	[Fact]
	public async Task AppendStep_CreatesStepWithDefaults()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait();

		driver.StepCount.Should().Be(1);
		driver.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task AppendStep_MultipleSteps_IncreasesCount()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait().AddWait().AddWait();

		driver.StepCount.Should().Be(3);
		driver.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task InsertStep_AtBeginning_ShiftsExistingSteps()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(5f).AddWait(10f);

		// Insert at beginning
		driver.InsertWait(0, 15f);

		driver.StepCount.Should().Be(3);
		driver.Snapshot.StepStartTimes[0].Should().Be(TimeSpan.Zero);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(15));
		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public async Task InsertStep_InMiddle_ShiftsStartTimes()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(10f);

		var beforeSecond = driver.Snapshot.StepStartTimes[1];
		beforeSecond.Should().Be(TimeSpan.FromSeconds(10));

		// Insert in the middle
		driver.InsertWait(1, 5f);

		driver.StepCount.Should().Be(3);
		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public async Task RemoveStep_RecalculatesStartTimes()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(10f).AddWait(10f);

		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(20));

		driver.RemoveStep(1);

		driver.StepCount.Should().Be(2);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task RemoveStep_LastStep_LeavesEmptyRecipe()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait();

		driver.StepCount.Should().Be(1);

		driver.RemoveStep(0);

		driver.StepCount.Should().Be(0);
	}

	[Fact]
	public async Task ReplaceAction_LongLastingToImmediate_RemovesDurationEffect()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		const float CustomDuration = 12f;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(CustomDuration);

		var before = driver.Snapshot.TotalDuration;
		before.Should().Be(TimeSpan.FromSeconds(CustomDuration));

		// Replace Wait (longlasting) with ForLoop (immediate)
		driver.ReplaceAction(0, RecipeTestDriver.ForLoopActionId);

		var after = driver.Snapshot.TotalDuration;
		after.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task ReplaceAction_ImmediateToLongLasting_AddsDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddPause();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);

		// Replace Pause (immediate) with Wait (longlasting)
		driver.ReplaceAction(0, RecipeTestDriver.WaitActionId);
		driver.SetDuration(0, 15f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public async Task UpdateProperty_ChangesDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(10));

		driver.SetDuration(0, 25f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(25));
	}

	[Fact]
	public async Task UpdateProperty_InvalidIndex_Throws()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		var act = () => driver.SetDuration(5, 10f);

		act.Should().Throw<Exception>();
	}

	[Fact]
	public async Task NewRecipe_ResetsToEmpty()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait().AddWait().AddWait();

		driver.StepCount.Should().Be(3);

		driver.NewRecipe();

		driver.StepCount.Should().Be(0);
	}
}
