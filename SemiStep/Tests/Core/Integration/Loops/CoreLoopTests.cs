using Domain.State;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Loops;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Loops")]
public sealed class CoreLoopTests
{
	private const int SingleIterationDuration = 4;
	private const int DefaultIterationCount = 3;
	private const int MaxAllowedNestingDepth = 3;

	[Fact]
	public async Task ClosedLoop_IsValid()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(DefaultIterationCount).AddWait(SingleIterationDuration).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(1);
	}

	[Fact]
	public async Task ClosedLoop_ComputesIterationTiming()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(DefaultIterationCount).AddWait(SingleIterationDuration).AddEndFor();

		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(SingleIterationDuration * DefaultIterationCount));

		var loop = driver.Snapshot.LoopByStart[0];
		loop.Iterations.Should().Be(DefaultIterationCount);
	}

	[Fact]
	public async Task UnclosedLoop_InvalidatesRecipe()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(2).AddWait(5f);

		driver.IsValid.Should().BeFalse();
		driver.Snapshot.Errors.Should().NotBeEmpty();
	}

	[Fact]
	public async Task UnmatchedEndFor_InvalidatesRecipe()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(5f).AddEndFor();

		driver.IsValid.Should().BeFalse();
		driver.Snapshot.Errors.Should().NotBeEmpty();
	}

	[Fact]
	public async Task NestedLoops_ComputeCorrectDepth()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Outer loop (depth 1)
		driver.AddFor(2);
		// Inner loop (depth 2)
		driver.AddFor(3);
		driver.AddWait(5f);
		driver.AddEndFor(); // End inner
		driver.AddEndFor(); // End outer

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(2);

		// Check depths
		var outerLoop = driver.Snapshot.LoopByStart[0];
		var innerLoop = driver.Snapshot.LoopByStart[1];

		outerLoop.Depth.Should().Be(1);
		innerLoop.Depth.Should().Be(2);
	}

	[Fact]
	public async Task NestedLoops_ComputeCorrectTiming()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		const int OuterIterations = 2;
		const int InnerIterations = 3;
		const int StepDuration = 5;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(OuterIterations);
		driver.AddFor(InnerIterations);
		driver.AddWait(StepDuration);
		driver.AddEndFor();
		driver.AddEndFor();

		// Total = OuterIterations * InnerIterations * StepDuration = 2 * 3 * 5 = 30
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public async Task MaxDepthExceeded_HasError()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Create MaxAllowedNestingDepth + 1 nested loops
		for (var i = 0; i <= MaxAllowedNestingDepth; i++)
		{
			driver.AddFor(1);
		}

		driver.AddWait(2f);

		// Close all loops
		for (var i = 0; i <= MaxAllowedNestingDepth; i++)
		{
			driver.AddEndFor();
		}

		driver.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task LoopByStart_LoopByEnd_CorrectMapping()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(2).AddWait(5f).AddEndFor();

		driver.Snapshot.LoopByStart.Should().ContainKey(0);
		driver.Snapshot.LoopByEnd.Should().ContainKey(2);

		var loopByStart = driver.Snapshot.LoopByStart[0];
		var loopByEnd = driver.Snapshot.LoopByEnd[2];

		loopByStart.Should().Be(loopByEnd);
	}

	[Fact]
	public async Task EnclosingLoopsMap_CorrectlyBuilt()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Step 0: For (outer)
		// Step 1: For (inner)
		// Step 2: Wait
		// Step 3: EndFor (inner)
		// Step 4: EndFor (outer)
		driver.AddFor(2).AddFor(3).AddWait(5f).AddEndFor().AddEndFor();

		// Step 2 (Wait) should be enclosed by both loops
		driver.Snapshot.EnclosingLoops.Should().ContainKey(2);
		driver.Snapshot.EnclosingLoops[2].Should().HaveCount(2);
	}

	[Fact]
	public async Task EmptyLoop_ValidButZeroDuration()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(5).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task MultipleSequentialLoops_Valid()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// First loop
		driver.AddFor(2).AddWait(5f).AddEndFor();
		// Second loop
		driver.AddFor(3).AddWait(10f).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(2);

		// Total = 2*5 + 3*10 = 10 + 30 = 40
		driver.Snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(40));
	}
}
