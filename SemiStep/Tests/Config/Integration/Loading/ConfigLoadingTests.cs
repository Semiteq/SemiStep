using FluentAssertions;

using Tests.Config.Helpers;

using Xunit;

namespace Tests.Config.Integration.Loading;

/// <summary>
/// Tests for successful config loading scenarios (happy path).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Feature", "Loading")]
public class ConfigLoadingTests
{
	[Fact]
	public async Task StandardConfig_LoadsSuccessfully()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		config.Should().NotBeNull();
	}

	[Fact]
	public async Task StandardConfig_HasProperties()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		config.Properties.Should().NotBeNull();
		config.Properties.Should().NotBeEmpty();
	}

	[Fact]
	public async Task StandardConfig_HasColumns()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		config.Columns.Should().NotBeNull();
		config.Columns.Should().NotBeEmpty();
	}

	[Fact]
	public async Task StandardConfig_HasActions()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		config.Actions.Should().NotBeNull();
		config.Actions.Should().NotBeEmpty();
	}

	[Fact]
	public async Task StandardConfig_HasExpectedPropertyTypes()
	{
		// Arrange
		var expectedPropertyTypeIds = new[] { "int", "float", "string", "enum", "time" };

		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		foreach (var expectedId in expectedPropertyTypeIds)
		{
			config.Properties.Values.Should()
				.Contain(p => p.PropertyTypeId == expectedId,
					$"expected property type '{expectedId}' should exist");
		}
	}

	[Fact]
	public async Task StandardConfig_HasExpectedColumns()
	{
		// Arrange
		var expectedColumnKeys = new[] { "action", "step_duration", "task", "comment" };

		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		foreach (var expectedKey in expectedColumnKeys)
		{
			config.Columns.Should()
				.Contain(c => c.Key == expectedKey,
					$"expected column '{expectedKey}' should exist");
		}
	}

	[Fact]
	public async Task StandardConfig_HasExpectedActions()
	{
		// Arrange - action Ids: Wait=10, For=20, EndFor=30, Pause=40
		var expectedActionIds = new int[] { 10, 20, 30, 40 };

		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		foreach (var expectedId in expectedActionIds)
		{
			config.Actions.Values.Should()
				.Contain(a => a.Id == expectedId,
					$"expected action with Id={expectedId} should exist");
		}
	}

	[Fact]
	public async Task StandardConfig_ActionHasCorrectColumns()
	{
		// Arrange - Wait action (Id=10) should have step_duration and comment columns

		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		var waitAction = config.Actions.Values.FirstOrDefault(a => a.Id == 10);
		waitAction.Should().NotBeNull();
		waitAction!.Columns.Should().Contain(c => c.Key == "step_duration");
		waitAction.Columns.Should().Contain(c => c.Key == "comment");
	}

	[Fact]
	public async Task StandardConfig_PropertyHasCorrectSystemType()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		var intProperty = config.Properties.Values.FirstOrDefault(p => p.PropertyTypeId == "int");
		intProperty.Should().NotBeNull();
		intProperty!.SystemType.Should().Be("int");

		var floatProperty = config.Properties.Values.FirstOrDefault(p => p.PropertyTypeId == "float");
		floatProperty.Should().NotBeNull();
		floatProperty!.SystemType.Should().Be("float");

		var stringProperty = config.Properties.Values.FirstOrDefault(p => p.PropertyTypeId == "string");
		stringProperty.Should().NotBeNull();
		stringProperty!.SystemType.Should().Be("string");
	}

	[Fact]
	public async Task StandardConfig_TimePropertyHasMinMax()
	{
		// Act
		var config = await ConfigTestHelper.LoadValidCaseAsync();

		// Assert
		var timeProperty = config.Properties.Values.FirstOrDefault(p => p.PropertyTypeId == "time");
		timeProperty.Should().NotBeNull();
		timeProperty!.Min.Should().Be(0);
		timeProperty.Max.Should().Be(86400);
	}

	[Fact]
	public async Task StandardConfig_NoErrors()
	{
		// Arrange
		using var tempDir = TestDataCopier.PrepareValidCase();
		var facade = ConfigTestHelper.CreateFacade();

		// Act
		var context = await facade.LoadAsync(tempDir.Path);

		// Assert
		context.HasErrors.Should().BeFalse(
			$"expected no errors but got: {string.Join(", ", context.Errors.Select(e => e.Message))}");
	}

	[Fact]
	public async Task StandardConfig_NoWarnings()
	{
		// Arrange
		using var tempDir = TestDataCopier.PrepareValidCase();
		var facade = ConfigTestHelper.CreateFacade();

		// Act
		var context = await facade.LoadAsync(tempDir.Path);

		// Assert
		context.HasWarnings.Should().BeFalse(
			$"expected no warnings but got: {string.Join(", ", context.Warnings.Select(w => w.Message))}");
	}
}
