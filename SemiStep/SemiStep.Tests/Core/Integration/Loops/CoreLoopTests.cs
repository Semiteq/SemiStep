using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Loops;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Loops")]
public sealed class CoreLoopTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	private const int SingleIterationDuration = 4;
	private const int DefaultIterationCount = 3;
	private const int MaxAllowedNestingDepth = 3;

	[Fact]
	public void ClosedLoop_IsValid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(DefaultIterationCount).AddWait(SingleIterationDuration).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(1);
	}

	[Fact]
	public void ClosedLoop_ComputesIterationTiming()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(DefaultIterationCount).AddWait(SingleIterationDuration).AddEndFor();

		driver.Snapshot.TotalDuration.Should()
			.Be(TimeSpan.FromSeconds(SingleIterationDuration * DefaultIterationCount));

		var loop = driver.Snapshot.LoopByStart[0];
		loop.Iterations.Should().Be(DefaultIterationCount);
	}

	[Fact]
	public void UnclosedLoop_ProducesWarning()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(2).AddWait(5f);

		driver.Warnings.Should().NotBeEmpty();
	}

	[Fact]
	public void UnmatchedEndFor_ProducesWarning()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(5f).AddEndFor();

		driver.Warnings.Should().NotBeEmpty();
	}

	[Fact]
	public void NestedLoops_ComputeCorrectDepth()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(2);
		driver.AddFor(3);
		driver.AddWait(5f);
		driver.AddEndFor();
		driver.AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(2);

		var outerLoop = driver.Snapshot.LoopByStart[0];
		var innerLoop = driver.Snapshot.LoopByStart[1];

		outerLoop.Depth.Should().Be(1);
		innerLoop.Depth.Should().Be(2);
	}

	[Fact]
	public void NestedLoops_ComputeCorrectTiming()
	{
		fixture.Session.Reset();

		const int OuterIterations = 2;
		const int InnerIterations = 3;
		const int StepDuration = 5;

		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(OuterIterations);
		driver.AddFor(InnerIterations);
		driver.AddWait(StepDuration);
		driver.AddEndFor();
		driver.AddEndFor();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void MaxDepthExceeded_RejectsMutation()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		for (var i = 0; i <= MaxAllowedNestingDepth; i++)
		{
			driver.AddFor(1);
		}

		driver.AddWait(1f);

		var stepCountBeforeRejection = driver.StepCount;
		var result = fixture.Session.AppendStep(RecipeTestDriver.EndForLoopActionId);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e.Message.Contains("nesting depth", StringComparison.OrdinalIgnoreCase));
		driver.IsValid.Should().BeTrue("the mutation was rejected, recipe is unchanged");
		driver.StepCount.Should().Be(stepCountBeforeRejection);
	}

	[Fact]
	public void LoopByStart_LoopByEnd_CorrectMapping()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(2).AddWait(5f).AddEndFor();

		driver.Snapshot.LoopByStart.Should().ContainKey(0);
		driver.Snapshot.LoopByEnd.Should().ContainKey(2);

		var loopByStart = driver.Snapshot.LoopByStart[0];
		var loopByEnd = driver.Snapshot.LoopByEnd[2];

		loopByStart.Should().Be(loopByEnd);
	}

	[Fact]
	public void EnclosingLoopsMap_CorrectlyBuilt()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(2).AddFor(3).AddWait(5f).AddEndFor().AddEndFor();

		driver.Snapshot.EnclosingLoops.Should().ContainKey(2);
		driver.Snapshot.EnclosingLoops[2].Should().HaveCount(2);
	}

	[Fact]
	public void EmptyLoop_ValidButZeroDuration()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(5).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void MultipleSequentialLoops_Valid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(2).AddWait(5f).AddEndFor();
		driver.AddFor(3).AddWait(10f).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(2);

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(40));
	}
}
