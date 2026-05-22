using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Loops;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Loops")]
public sealed class RecipeSnapshotRowLoopDepthsTests
{
	private const int DummyActionId = 1;

	[Fact]
	public void NoLoops_AllZeros()
	{
		var recipe = BuildRecipe(3);
		var snapshot = BuildSnapshot(recipe, Array.Empty<LoopInfo>());

		snapshot.RowLoopDepths.Should().Equal(0, 0, 0);
	}

	[Fact]
	public void SingleLoop_TintsAllInclusiveRows()
	{
		var recipe = BuildRecipe(3);
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 2)
		};

		var snapshot = BuildSnapshot(recipe, loops);

		snapshot.RowLoopDepths.Should().Equal(1, 1, 1);
	}

	[Fact]
	public void NestedLoops_InnerRowsCarryDeeperTint()
	{
		var recipe = BuildRecipe(5);
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 4, Depth: 1, Iterations: 2),
			new LoopInfo(StartIndex: 1, EndIndex: 3, Depth: 2, Iterations: 2)
		};

		var snapshot = BuildSnapshot(recipe, loops);

		snapshot.RowLoopDepths.Should().Equal(1, 2, 2, 2, 1);
	}

	[Fact]
	public void AbuttingLoops_TintEachIndependently()
	{
		var recipe = BuildRecipe(6);
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 2, Depth: 1, Iterations: 2),
			new LoopInfo(StartIndex: 3, EndIndex: 5, Depth: 1, Iterations: 2)
		};

		var snapshot = BuildSnapshot(recipe, loops);

		snapshot.RowLoopDepths.Should().Equal(1, 1, 1, 1, 1, 1);
	}

	[Fact]
	public void DeepNesting_NotCappedAtCoreLevel()
	{
		var recipe = BuildRecipe(8);
		var loops = new[]
		{
			new LoopInfo(StartIndex: 0, EndIndex: 7, Depth: 1, Iterations: 2),
			new LoopInfo(StartIndex: 1, EndIndex: 6, Depth: 2, Iterations: 2),
			new LoopInfo(StartIndex: 2, EndIndex: 5, Depth: 3, Iterations: 2),
			new LoopInfo(StartIndex: 3, EndIndex: 4, Depth: 4, Iterations: 2)
		};

		var snapshot = BuildSnapshot(recipe, loops);

		snapshot.RowLoopDepths.Should().Equal(1, 2, 3, 4, 4, 3, 2, 1);
	}

	private static Recipe BuildRecipe(int stepCount)
	{
		var steps = ImmutableList.CreateBuilder<Step>();
		for (var i = 0; i < stepCount; i++)
		{
			steps.Add(new Step(DummyActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty));
		}

		return new Recipe(steps.ToImmutable());
	}

	private static RecipeSnapshot BuildSnapshot(Recipe recipe, IReadOnlyList<LoopInfo> loops)
	{
		return RecipeSnapshot.Create(
			recipe,
			TimeSpan.Zero,
			new Dictionary<int, TimeSpan>(),
			loops,
			new Dictionary<int, TimeSpan>());
	}
}
