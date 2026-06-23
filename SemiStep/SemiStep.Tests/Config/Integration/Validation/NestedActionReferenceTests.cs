using FluentAssertions;

using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Validation;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "NestedActions")]
public sealed class NestedActionReferenceTests
{
	[Fact]
	public async Task DanglingTarget_FailsWithClearMessage()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NestedDanglingTarget");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("targets undefined action id 9999", StringComparison.OrdinalIgnoreCase));
		result.Errors.Should().Contain(e =>
			e.Message.Contains("branch_sel", StringComparison.OrdinalIgnoreCase),
			"error should identify the selector column carrying the dangling target");
	}

	[Fact]
	public async Task TargetPointingAtAction_FailsWithClearMessage()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NestedTargetIsAction");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("which is role 'action'", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("must point at a 'subaction'", StringComparison.OrdinalIgnoreCase));
		result.Errors.Should().Contain(e =>
			e.Message.Contains("targets action id 10", StringComparison.OrdinalIgnoreCase),
			"error should identify the mis-tagged target id");
	}

	[Fact]
	public async Task OrphanSubaction_FailsWithClearMessage()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NestedOrphanSubaction");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("orphan subaction", StringComparison.OrdinalIgnoreCase));
		result.Errors.Should().Contain(e =>
			e.Message.Contains("3001", StringComparison.OrdinalIgnoreCase),
			"error should identify the unreferenced subaction id");
	}

	[Fact]
	public async Task DuplicateSubactionId_FailsWithClearMessage()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NestedDuplicateId");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Duplicate action Id", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("3001", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task SharedColumnWithinRoot_FailsWithClearMessage()
	{
		var result = await ConfigTestHelper.LoadInvalidCaseAsync("NestedSharedWithinRoot");

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("more than one selector condition", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("sub_value", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ValidNestedConfig_Passes()
	{
		var config = await ConfigTestHelper.LoadStandaloneCaseAsync("NestedActionsValid");

		config.IsSuccess.Should().BeTrue(
			"a nested config whose targets resolve to a referenced subaction must load");
	}
}
