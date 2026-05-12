using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Targets;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "TargetEdgeCases")]
public sealed class CoreTargetsEdgeCasesTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void GetActionByName_EmptyName_Fails()
	{
		var recipeMetadataRegistry = fixture.Services.GetRequiredService<RecipeMetadataRegistry>();

		var result = recipeMetadataRegistry.GetActionByName("");

		result.IsFailed.Should().BeTrue("empty string does not match any registered action name");
	}

	[Fact]
	public void GetGroup_InvalidId_Fails()
	{
		var recipeMetadataRegistry = fixture.Services.GetRequiredService<RecipeMetadataRegistry>();

		var result = recipeMetadataRegistry.GetGroup("nonexistent");

		result.IsFailed.Should().BeTrue("no group is registered with the given ID");
	}
}
