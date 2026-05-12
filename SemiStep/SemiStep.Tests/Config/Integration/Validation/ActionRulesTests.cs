using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ActionRulesValidation")]
public sealed class ActionRulesTests
{
	[Fact]
	public async Task EnumWithoutGroupName_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("EnumWithoutGroupName");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("group_name", StringComparison.OrdinalIgnoreCase));
	}
}
