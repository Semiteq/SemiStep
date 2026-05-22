using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Mutation;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Mutation")]
public sealed class CoreMutationTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void AppendStep_CreatesStepWithDefaults()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait();

		driver.StepCount.Should().Be(1);
		driver.IsValid.Should().BeTrue();
	}

	[Fact]
	public void AppendStep_MultipleSteps_IncreasesCount()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait().AddWait().AddWait();

		driver.StepCount.Should().Be(3);
		driver.IsValid.Should().BeTrue();
	}

	[Fact]
	public void InsertStep_AtBeginning_ShiftsExistingSteps()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(5f).AddWait(10f);

		driver.InsertWait(0, 15f);

		driver.StepCount.Should().Be(3);
		driver.Snapshot.StepStartTimes[0].Should().Be(TimeSpan.Zero);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(15));
		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void InsertStep_InMiddle_ShiftsStartTimes()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f).AddWait(10f);

		var beforeSecond = driver.Snapshot.StepStartTimes[1];
		beforeSecond.Should().Be(TimeSpan.FromSeconds(10));

		driver.InsertWait(1, 5f);

		driver.StepCount.Should().Be(3);
		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void RemoveStep_RecalculatesStartTimes()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f).AddWait(10f).AddWait(10f);

		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(20));

		driver.RemoveStep(1);

		driver.StepCount.Should().Be(2);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void RemoveStep_LastStep_LeavesEmptyRecipe()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait();

		driver.StepCount.Should().Be(1);

		driver.RemoveStep(0);

		driver.StepCount.Should().Be(0);
	}

	[Fact]
	public void ReplaceAction_LongLastingToImmediate_RemovesDurationEffect()
	{
		fixture.Session.Reset();

		const float CustomDuration = 12f;

		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(CustomDuration);

		var before = driver.Snapshot.TotalDuration;
		before.Should().Be(TimeSpan.FromSeconds(CustomDuration));

		driver.ReplaceAction(0, RecipeTestDriver.ForLoopActionId);

		var after = driver.Snapshot.TotalDuration;
		after.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void ReplaceAction_ImmediateToLongLasting_AddsDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddPause();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);

		driver.ReplaceAction(0, RecipeTestDriver.WaitActionId);
		driver.SetDuration(0, 15f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void UpdateProperty_ChangesDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(10));

		driver.SetDuration(0, 25f);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(25));
	}

	[Fact]
	public void UpdateProperty_InvalidIndex_DoesNotModifyState()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		var result = fixture.Session.UpdateStepProperty(5, RecipeTestDriver.StepDurationColumn, "10");

		result.IsFailed.Should().BeTrue();
		driver.StepCount.Should().Be(0);
	}

	[Fact]
	public void NewRecipe_ResetsToEmpty()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait().AddWait().AddWait();

		driver.StepCount.Should().Be(3);

		driver.NewRecipe();

		driver.StepCount.Should().Be(0);
	}

	[Fact]
	public void InsertSteps_InsertsMultipleStepsAtPosition()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(5f).AddWait(10f);

		var stepsToInsert = new List<Step>
		{
			new(RecipeTestDriver.WaitActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(RecipeTestDriver.StepDurationColumn), PropertyValue.FromFloat(20f))),
			new(RecipeTestDriver.WaitActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(RecipeTestDriver.StepDurationColumn), PropertyValue.FromFloat(30f)))
		};

		driver.InsertSteps(1, stepsToInsert);

		driver.StepCount.Should().Be(4);
		driver.Snapshot.StepStartTimes[0].Should().Be(TimeSpan.Zero);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(5));
		driver.Snapshot.StepStartTimes[2].Should().Be(TimeSpan.FromSeconds(25));
		driver.Snapshot.StepStartTimes[3].Should().Be(TimeSpan.FromSeconds(55));
	}

	[Fact]
	public void InsertSteps_AtEnd_AppendsSteps()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(5f);

		var stepsToInsert = new List<Step>
		{
			new(RecipeTestDriver.WaitActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(RecipeTestDriver.StepDurationColumn), PropertyValue.FromFloat(15f)))
		};

		driver.InsertSteps(1, stepsToInsert);

		driver.StepCount.Should().Be(2);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(5));
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void RemoveSteps_NonContiguousIndices_RemovesCorrectSteps()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(5f).AddWait(10f).AddWait(15f).AddWait(20f);

		driver.StepCount.Should().Be(4);

		driver.RemoveSteps([0, 2]);

		driver.StepCount.Should().Be(2);
		driver.Snapshot.StepStartTimes[0].Should().Be(TimeSpan.Zero);
		driver.Snapshot.StepStartTimes[1].Should().Be(TimeSpan.FromSeconds(10));
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void RemoveSteps_AllSteps_LeavesEmptyRecipe()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait().AddWait().AddWait();

		driver.RemoveSteps([0, 1, 2]);

		driver.StepCount.Should().Be(0);
	}
}
