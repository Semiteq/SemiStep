using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Targets;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Targets")]
public sealed class CoreTargetsTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void Actions_List_NotEmpty()
	{
		var recipeMetadataRegistry = fixture.Services.GetRequiredService<RecipeMetadataRegistry>();
		var actions = recipeMetadataRegistry.GetAllActions();

		actions.Should().NotBeEmpty("Standard config defines at least 4 actions");
	}

	[Fact]
	public void EnumOptions_ForGroupColumn_Succeeds()
	{
		var recipeMetadataRegistry = fixture.Services.GetRequiredService<RecipeMetadataRegistry>();
		var groupResult = recipeMetadataRegistry.GetGroup("valve");

		groupResult.IsSuccess.Should().BeTrue();
		groupResult.Value.Items.Should().NotBeEmpty("WithGroups config defines a valve group with items");
	}

	[Fact]
	public void GroupExists_ForDefinedGroup_ReturnsTrue()
	{
		var recipeMetadataRegistry = fixture.Services.GetRequiredService<RecipeMetadataRegistry>();
		var exists = recipeMetadataRegistry.GroupExists("valve");

		exists.IsSuccess.Should().BeTrue("valve group is defined in WithGroups config");
	}
}
