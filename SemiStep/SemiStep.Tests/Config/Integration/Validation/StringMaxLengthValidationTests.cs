using AwesomeAssertions;

using SemiStep.Core.Configuration.Validation;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "StringMaxLengthValidation")]
public sealed class StringMaxLengthValidationTests
{
	[Fact]
	public async Task NoStringProperty_FailsWithNoStringPropertyError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("StringMaxLengthNoStringProperty");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle().Which.Should().BeOfType<NoStringPropertyError>();
	}

	[Fact]
	public async Task StringPropertiesWithoutMaxLength_FailOncePerProperty()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("StringMaxLengthMissing");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCount(2).And.AllBeOfType<StringMaxLengthMissingError>();
		result.Errors.OfType<StringMaxLengthMissingError>().Select(error => error.PropertyTypeId)
			.Should().BeEquivalentTo(["string", "note"]);
	}

	[Fact]
	public async Task StringPropertyWithZeroMaxLength_FailsNamingThePropertyAndValue()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("StringMaxLengthNotPositive");

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.Should().ContainSingle()
			.Which.Should().BeOfType<StringMaxLengthNotPositiveError>().Subject;
		error.PropertyTypeId.Should().Be("string");
		error.MaxLength.Should().Be(0);
	}

	[Fact]
	public async Task StringPropertiesWithDistinctMaxLengths_FailWithBothValues()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("StringMaxLengthConflict");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle().Which.Should().BeOfType<StringMaxLengthConflictError>()
			.Which.MaxLengths.Should().BeEquivalentTo([255, 64]);
	}

	[Fact]
	public async Task MaxLengthDefectWithUnknownColumnReference_ReportsBothInOneResult()
	{
		var referenceOnly = await ConfigTestHelper.LoadInvalidCaseAsync("MissingPropertyReference");
		var combined = await ConfigTestHelper.LoadInvalidCaseAsync("StringMaxLengthWithMissingReference");

		combined.IsFailed.Should().BeTrue();
		combined.Errors.OfType<StringMaxLengthNotPositiveError>().Should().ContainSingle();
		combined.Errors.Should().HaveCount(referenceOnly.Errors.Count + 1);
	}
}
