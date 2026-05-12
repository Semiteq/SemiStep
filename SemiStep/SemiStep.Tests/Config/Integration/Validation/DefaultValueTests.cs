using FluentAssertions;

using SemiStep.Core.Shared;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "DefaultValueValidation")]
public sealed class DefaultValueTests
{
	[Fact]
	public async Task StringDefaultValue_ExceedsMaxLength_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DefaultValueStringTooLong");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("max_length", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("too long", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DefaultValue_NotParsable_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DefaultValueNotParsable");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task FloatDefaultValue_OutOfRange_HasError()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DefaultValueFloatOutOfRange");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("out of range", StringComparison.OrdinalIgnoreCase) ||
			e.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task DefaultValue_OnReadOnlyColumn_HasWarning()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("DefaultValueReadOnlyConflict");

		result.IsSuccess.Should().BeTrue("a default value on a readonly column is a warning, not an error");

		var warnings = result.Reasons.OfType<Warning>().Select(w => w.Message).ToList();
		warnings.Should().Contain(w =>
			w.Contains("read_only", StringComparison.OrdinalIgnoreCase) ||
			w.Contains("readonly", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task FloatDefaultValue_ParsedWithInvariantCulture_UnderRuRu_Succeeds()
	{
		using var _ = new CultureScope("ru-RU");

		var result = await ConfigTestHelper.LoadStandaloneCaseAsync("FloatCultureInvariant");

		result.IsSuccess.Should().BeTrue(
			"dot-decimal float values should parse correctly under ru-RU culture via InvariantCulture");
		result.Value.Actions.Should().NotBeEmpty();
	}
}
