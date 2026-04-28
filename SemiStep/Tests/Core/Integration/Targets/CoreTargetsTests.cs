using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Targets;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Targets")]
public sealed class CoreTargetsTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void Actions_List_NotEmpty()
	{
		var configRegistry = fixture.Services.GetRequiredService<ConfigRegistry>();
		var actions = configRegistry.GetAllActions();

		actions.Should().NotBeEmpty("Standard config defines at least 4 actions");
	}

	[Fact]
	public void EnumOptions_ForGroupColumn_Succeeds()
	{
		var configRegistry = fixture.Services.GetRequiredService<ConfigRegistry>();
		var groupResult = configRegistry.GetGroup("valve");

		groupResult.IsSuccess.Should().BeTrue();
		groupResult.Value.Items.Should().NotBeEmpty("WithGroups config defines a valve group with items");
	}

	[Fact]
	public void GroupExists_ForDefinedGroup_ReturnsTrue()
	{
		var configRegistry = fixture.Services.GetRequiredService<ConfigRegistry>();
		var exists = configRegistry.GroupExists("valve");

		exists.IsSuccess.Should().BeTrue("valve group is defined in WithGroups config");
	}
}
