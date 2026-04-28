using FluentAssertions;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Mutation;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "MutationEdgeCases")]
public sealed class CoreGroupValidationTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void UpdateProperty_ValidGroupKey_Succeeds()
	{
		fixture.Workspace.Reset();
		fixture.Editor.AppendStep(RecipeTestDriver.WithGroupActionId);

		var result = fixture.Editor.UpdateStepProperty(0, RecipeTestDriver.TargetColumn, "1");

		result.IsSuccess.Should().BeTrue("key 1 is a member of the valve group");
	}

	[Fact]
	public void UpdateProperty_InvalidGroupKey_ReturnsFail()
	{
		fixture.Workspace.Reset();
		fixture.Editor.AppendStep(RecipeTestDriver.WithGroupActionId);

		var result = fixture.Editor.UpdateStepProperty(0, RecipeTestDriver.TargetColumn, "99");

		result.IsFailed.Should().BeTrue("key 99 is not a member of the valve group");
	}
}
