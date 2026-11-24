using AwesomeAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Configuration.Validation;
using SemiStep.Core.Shared;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ActionTreeValidation")]
public sealed class ActionTreeValidationTests
{
	[Fact]
	public async Task EveryActionsFileEmpty_FailsWithNoPrimaryActionsError_AndKeepsTheWarning()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NoPrimaryActionsAllFilesEmpty");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle().Which.Should().BeOfType<NoPrimaryActionsError>();
		result.Reasons.OfType<Warning>().Should().NotBeEmpty();
	}

	[Fact]
	public async Task OneEmptyActionsFileAmongOthers_Loads()
	{
		using var tempDir = TestDataCopier.PrepareValidCase();
		TestDataCopier.WriteYaml(tempDir, "actions/empty.yaml", "# no actions in this file\n");

		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue();
		result.Value.Actions.Should().NotBeEmpty();
		result.Reasons.OfType<Warning>().Should().NotBeEmpty();
	}

	[Fact]
	public async Task OnlySubactionsInACycle_FailsBeforeTheZeroPrimaryCheck()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("SubactionsOnlyCycle");

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<NoPrimaryActionsError>().Should().BeEmpty();
	}

	[Fact]
	public async Task SameSubactionsChainedFromAPrimaryAction_Loads()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("SubactionsChainFromPrimaryAction");

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public async Task RepeatedColumnKeyWithConflictingPropertyTypes_FailsTheLoad()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("ActionDuplicateColumnKeyConflictingType");

		result.IsFailed.Should().BeTrue();
		result.Errors.OfType<NoPrimaryActionsError>().Should().BeEmpty();
	}

	[Fact]
	public async Task RepeatedColumnKeyWithEqualPropertyTypes_Loads()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("ActionDuplicateColumnKeyEqualType");

		result.IsSuccess.Should().BeTrue();
	}
}
