using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Validation;

/// <summary>
/// Tests for cross-reference validation between config sections.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "CrossReferenceValidation")]
public class CrossReferenceTests
{
	[Fact]
	public async Task MissingPropertyReference_InColumn_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingPropertyReference");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("unknown property_type_id", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingPropertyReference_IdentifiesUnknownId()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingPropertyReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("nonexistent_property", StringComparison.OrdinalIgnoreCase),
			"error should identify 'nonexistent_property' as the unknown property_type_id");
	}

	[Fact]
	public async Task MissingColumnReference_InAction_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingColumnReference");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("unknown column", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingColumnReference_IdentifiesUnknownColumn()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingColumnReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("nonexistent_column", StringComparison.OrdinalIgnoreCase),
			"error should identify 'nonexistent_column' as the unknown column");
	}

	[Fact]
	public async Task MissingGroupReference_InActionColumn_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingGroupReference");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("unknown group_name", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingGroupReference_IdentifiesUnknownGroup()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingGroupReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("nonexistent_group", StringComparison.OrdinalIgnoreCase),
			"error should identify 'nonexistent_group' as the unknown group_name");
	}

	[Fact]
	public async Task MissingActionPropertyReference_HasError()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingActionPropertyReference");

		// Assert
		context.HasErrors.Should().BeTrue();
		context.Errors.Should().Contain(e =>
			e.Message.Contains("unknown property_type_id", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task MissingActionPropertyReference_IdentifiesUnknownId()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingActionPropertyReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("nonexistent_property", StringComparison.OrdinalIgnoreCase),
			"error should identify 'nonexistent_property' as the unknown property_type_id");
	}

	[Fact]
	public async Task ValidCrossReferences_NoErrors()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert - if we get here without exception, cross-references are valid
		config.Should().NotBeNull();
		config.Actions.Should().NotBeEmpty("valid config should have actions");
		config.Columns.Should().NotBeEmpty("valid config should have columns");
		config.Properties.Should().NotBeEmpty("valid config should have properties");
	}

	[Fact]
	public async Task ErrorLocation_IncludesColumnKey()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingPropertyReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("step_duration"),
			"error should reference the column 'step_duration' that has the broken reference");
	}

	[Fact]
	public async Task ErrorLocation_IncludesActionInfo()
	{
		// Act
		var context = await ConfigTestHelper.LoadInvalidCaseAsync("MissingColumnReference");

		// Assert
		context.Errors.Should().Contain(e =>
			e.Message.Contains("Wait") || e.Message.Contains("10"),
			"error should reference the action 'Wait' (Id=10) that has the broken reference");
	}
}
