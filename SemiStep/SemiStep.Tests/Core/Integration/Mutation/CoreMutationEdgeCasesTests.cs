using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Mutation;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "MutationEdgeCases")]
public sealed class CoreMutationEdgeCasesTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	private const int InvalidNegativeIndex = -1;
	private const int InvalidLargeIndex = 100;

	[Fact]
	public void RemoveStep_NegativeIndex_Fails()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait();

		var result = fixture.Session.RemoveStep(InvalidNegativeIndex);

		result.IsFailed.Should().BeTrue();
		driver.StepCount.Should().Be(1, "state should not change on failed mutation");
	}

	[Fact]
	public void RemoveStep_IndexBeyondCount_Fails()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait();

		var result = fixture.Session.RemoveStep(InvalidLargeIndex);

		result.IsFailed.Should().BeTrue();
		driver.StepCount.Should().Be(1, "state should not change on failed mutation");
	}
}
