using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "PlcLayoutValidation")]
public sealed class PlcLayoutValidationTests
{
	[Fact]
	public async Task LoadAndValidateAsync_BrokenManagingDbLayout_FailsWithLayoutError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("BrokenManagingDbLayout");

		result.IsFailed.Should().BeTrue(
			"a broken managing DB layout must surface as a failed configuration result");
		result.Errors.Should().Contain(error => error.Message.Contains("ManagingDbLayout"),
			"PlcConfigurationValidator must produce an error tagged with the offending layout name");
	}
}
