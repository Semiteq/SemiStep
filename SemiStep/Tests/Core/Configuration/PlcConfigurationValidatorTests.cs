using FluentAssertions;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;

using Xunit;

namespace Tests.Core.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "PlcConfigurationValidation")]
public sealed class PlcConfigurationValidatorTests
{
	[Fact]
	public void Validate_DefaultConfiguration_Succeeds()
	{
		var config = PlcConfiguration.Default;

		var result = PlcConfigurationValidator.Validate(config);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_ManagingDbTotalSizeTooSmall_Fails()
	{
		var brokenManaging = ManagingDbLayout.Default with { TotalSize = 4 };
		var config = WithManaging(brokenManaging);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("ManagingDbLayout") &&
			error.Message.Contains("RecipeLinesOffset"));
	}

	[Fact]
	public void Validate_ManagingDbTotalSizeNotGreaterThanCommitted_Fails()
	{
		var brokenManaging = new ManagingDbLayout(
			DbNumber: 2,
			CommittedOffset: 8,
			RecipeLinesOffset: 0,
			TotalSize: 8);
		var config = WithManaging(brokenManaging);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("ManagingDbLayout") &&
			error.Message.Contains("CommittedOffset") &&
			error.Message.Contains("greater"));
	}

	[Fact]
	public void Validate_ManagingDbWithNegativeOffset_Fails()
	{
		var brokenManaging = ManagingDbLayout.Default with { CommittedOffset = -1 };
		var config = WithManaging(brokenManaging);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("CommittedOffset") &&
			error.Message.Contains("non-negative"));
	}

	[Fact]
	public void Validate_DataDbDataStartOverlapsHeader_Fails()
	{
		var brokenInt = DataDbLayout.DefaultInt with { DataStartOffset = 4 };
		var config = WithIntDb(brokenInt);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("IntDb") &&
			error.Message.Contains("DataStartOffset"));
	}

	[Fact]
	public void Validate_DataDbWithNegativeOffset_Fails()
	{
		var brokenFloat = DataDbLayout.DefaultFloat with { CapacityOffset = -4 };
		var config = WithFloatDb(brokenFloat);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("FloatDb") &&
			error.Message.Contains("CapacityOffset") &&
			error.Message.Contains("non-negative"));
	}

	[Fact]
	public void Validate_ExecutionDbTotalSizeOverflow_Fails()
	{
		var brokenExecution = ExecutionDbLayout.Default with { TotalSize = 6 };
		var config = WithExecution(brokenExecution);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("ExecutionDbLayout") &&
			error.Message.Contains("ForLoopCount1Offset"));
	}

	[Fact]
	public void Validate_ExecutionDbWithNegativeOffset_Fails()
	{
		var brokenExecution = ExecutionDbLayout.Default with { ActualLineOffset = -2 };
		var config = WithExecution(brokenExecution);

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("ExecutionDbLayout") &&
			error.Message.Contains("ActualLineOffset") &&
			error.Message.Contains("non-negative"));
	}

	[Fact]
	public void Validate_MultipleViolationsAcrossLayouts_AggregatesAllErrors()
	{
		var brokenManaging = new ManagingDbLayout(
			DbNumber: 2,
			CommittedOffset: -1,
			RecipeLinesOffset: 2,
			TotalSize: 4);
		var brokenInt = DataDbLayout.DefaultInt with { DataStartOffset = 4 };
		var brokenExecution = ExecutionDbLayout.Default with { TotalSize = 6 };

		var brokenLayout = new PlcProtocolLayout(
			ManagingDb: brokenManaging,
			IntDb: brokenInt,
			FloatDb: DataDbLayout.DefaultFloat,
			StringDb: DataDbLayout.DefaultString,
			ExecutionDb: brokenExecution);

		var config = PlcConfiguration.Default with { Layout = brokenLayout };

		var result = PlcConfigurationValidator.Validate(config);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error => error.Message.Contains("ManagingDbLayout"));
		result.Errors.Should().Contain(error => error.Message.Contains("IntDb"));
		result.Errors.Should().Contain(error => error.Message.Contains("ExecutionDbLayout"));
		result.Errors.Should().HaveCountGreaterThan(2);
	}

	private static PlcConfiguration WithManaging(ManagingDbLayout managing)
	{
		var layout = PlcProtocolLayout.Default with { ManagingDb = managing };
		return PlcConfiguration.Default with { Layout = layout };
	}

	private static PlcConfiguration WithIntDb(DataDbLayout intDb)
	{
		var layout = PlcProtocolLayout.Default with { IntDb = intDb };
		return PlcConfiguration.Default with { Layout = layout };
	}

	private static PlcConfiguration WithFloatDb(DataDbLayout floatDb)
	{
		var layout = PlcProtocolLayout.Default with { FloatDb = floatDb };
		return PlcConfiguration.Default with { Layout = layout };
	}

	private static PlcConfiguration WithExecution(ExecutionDbLayout execution)
	{
		var layout = PlcProtocolLayout.Default with { ExecutionDb = execution };
		return PlcConfiguration.Default with { Layout = layout };
	}
}
