using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Loops;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "LoopEdgeCases")]
public sealed class CoreLoopEdgeCasesTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void ZeroIterations_LoopStillValid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(0).AddWait(5f).AddEndFor();

		driver.IsValid.Should().BeTrue("a zero-iteration loop is structurally valid");
		driver.Snapshot.Loops.Should().HaveCount(1);
		driver.Snapshot.Loops[0].Iterations.Should().Be(0);
	}

	[Fact]
	public void NegativeIterations_LoopStillValid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(-5).AddWait(5f).AddEndFor();

		driver.IsValid.Should().BeTrue("a negative-iteration loop is structurally valid");
		driver.Snapshot.Loops.Should().HaveCount(1);
		driver.Snapshot.Loops[0].Iterations.Should().Be(-5);
	}

	[Fact]
	public void EnclosingLoops_OrderedOuterToInner()
	{
		fixture.Session.Reset();

		var driver = new RecipeTestDriver(fixture.Session);
		driver
			.AddFor(3)
			.AddFor(2)
			.AddWait(1f)
			.AddEndFor()
			.AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Loops.Should().HaveCount(2);

		var enclosing = driver.Snapshot.EnclosingLoops[2];
		enclosing.Should().HaveCount(2);

		enclosing[0].Depth.Should().BeLessThan(enclosing[1].Depth);
		enclosing[0].StartIndex.Should().Be(0, "outer loop starts at index 0");
		enclosing[1].StartIndex.Should().Be(1, "inner loop starts at index 1");
	}
}
