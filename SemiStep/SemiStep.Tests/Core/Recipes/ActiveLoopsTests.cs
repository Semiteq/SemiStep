using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;

using Xunit;

namespace SemiStep.Tests.Core.Recipes;

[Trait("Component", "Core")]
[Trait("Area", "ActiveLoops")]
[Trait("Category", "Unit")]
public sealed class ActiveLoopsTests
{
	private static readonly LoopInfo _outer = new(StartIndex: 0, EndIndex: 9, Depth: 1, Iterations: 5);
	private static readonly LoopInfo _inner = new(StartIndex: 2, EndIndex: 6, Depth: 2, Iterations: 3);
	private static readonly RecipeSnapshot _snapshot = RecipeSnapshot.Empty with { Loops = [_outer, _inner] };

	[Fact]
	public void InsideNestedLoops_ReturnsEveryLoopInnermostFirst_WithItsPass()
	{
		ActiveLoops.Resolve(_snapshot, Running(line: 4, depth1: 2, depth2: 1))
			.Should().Equal(new ActiveLoop(_inner, 1), new ActiveLoop(_outer, 2));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(6)]
	public void OnTheLoopsOwnStartOrEndRow_CountsThatLoop(int line)
	{
		ActiveLoops.Resolve(_snapshot, Running(line, depth1: 2, depth2: 3))
			.Select(active => active.Loop).Should().Equal(_inner, _outer);
	}

	[Fact]
	public void ALevelWithAZeroCounter_IsLeftOut()
	{
		ActiveLoops.Resolve(_snapshot, Running(line: 4, depth1: 2, depth2: 0))
			.Should().Equal(new ActiveLoop(_outer, 2));
	}

	[Fact]
	public void OutsideTheInnerLoop_ReturnsTheOuterOnly()
	{
		ActiveLoops.Resolve(_snapshot, Running(line: 8, depth1: 4, depth2: 3))
			.Should().Equal(new ActiveLoop(_outer, 4));
	}

	[Fact]
	public void RecipeNotRunning_ReturnsNone()
	{
		ActiveLoops.Resolve(_snapshot, Running(line: 4, depth1: 2, depth2: 1) with { RecipeActive = false })
			.Should().BeEmpty();
	}

	private static PlcExecutionInfo Running(int line, int depth1, int depth2)
	{
		return new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: line,
			StepCurrentTime: 0f,
			ForLoopCount1: depth1,
			ForLoopCount2: depth2,
			ForLoopCount3: 0);
	}
}
