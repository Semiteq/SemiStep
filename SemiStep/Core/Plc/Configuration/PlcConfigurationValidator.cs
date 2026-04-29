using FluentResults;

using SemiStep.Core.Plc.Configuration.Memory;

namespace SemiStep.Core.Plc.Configuration;

internal static class PlcConfigurationValidator
{
	public static Result Validate(PlcConfiguration config)
	{
		var validationResults = new List<Result>();

		ValidateManagingDb(config.Layout.ManagingDb, validationResults);
		ValidateDataDb(config.Layout.IntDb, nameof(PlcProtocolLayout.IntDb), validationResults);
		ValidateDataDb(config.Layout.FloatDb, nameof(PlcProtocolLayout.FloatDb), validationResults);
		ValidateDataDb(config.Layout.StringDb, nameof(PlcProtocolLayout.StringDb), validationResults);
		ValidateExecutionDb(config.Layout.ExecutionDb, validationResults);

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
	}

	private static void ValidateManagingDb(ManagingDbLayout layout, List<Result> validationResults)
	{
		const string LayoutName = nameof(ManagingDbLayout);

		ValidateNonNegativeOffset(layout.CommittedOffset, LayoutName, nameof(layout.CommittedOffset), validationResults);
		ValidateNonNegativeOffset(layout.RecipeLinesOffset, LayoutName, nameof(layout.RecipeLinesOffset), validationResults);
		ValidateNonNegativeOffset(layout.TotalSize, LayoutName, nameof(layout.TotalSize), validationResults);

		var requiredForRecipeLines = layout.RecipeLinesOffset + sizeof(int);
		if (layout.TotalSize < requiredForRecipeLines)
		{
			validationResults.Add(Result.Fail(
				$"{LayoutName}.{nameof(layout.TotalSize)} ({layout.TotalSize}) must be at least " +
				$"{nameof(layout.RecipeLinesOffset)} ({layout.RecipeLinesOffset}) + {sizeof(int)} bytes"));
		}

		if (layout.TotalSize <= layout.CommittedOffset)
		{
			validationResults.Add(Result.Fail(
				$"{LayoutName}.{nameof(layout.TotalSize)} ({layout.TotalSize}) must be greater than " +
				$"{nameof(layout.CommittedOffset)} ({layout.CommittedOffset})"));
		}
	}

	private static void ValidateDataDb(DataDbLayout layout, string layoutName, List<Result> validationResults)
	{
		ValidateNonNegativeOffset(layout.CapacityOffset, layoutName, nameof(layout.CapacityOffset), validationResults);
		ValidateNonNegativeOffset(layout.CurrentSizeOffset, layoutName, nameof(layout.CurrentSizeOffset), validationResults);
		ValidateNonNegativeOffset(layout.DataStartOffset, layoutName, nameof(layout.DataStartOffset), validationResults);

		var headerEnd = Math.Max(layout.CapacityOffset, layout.CurrentSizeOffset) + sizeof(int);
		if (layout.DataStartOffset < headerEnd)
		{
			validationResults.Add(Result.Fail(
				$"{layoutName}.{nameof(layout.DataStartOffset)} ({layout.DataStartOffset}) must be at least " +
				$"max({nameof(layout.CapacityOffset)}={layout.CapacityOffset}, " +
				$"{nameof(layout.CurrentSizeOffset)}={layout.CurrentSizeOffset}) + {sizeof(int)} bytes"));
		}
	}

	private static void ValidateExecutionDb(ExecutionDbLayout layout, List<Result> validationResults)
	{
		const string LayoutName = nameof(ExecutionDbLayout);

		ValidateNonNegativeOffset(layout.RecipeActiveOffset, LayoutName, nameof(layout.RecipeActiveOffset), validationResults);
		ValidateNonNegativeOffset(layout.ActualLineOffset, LayoutName, nameof(layout.ActualLineOffset), validationResults);
		ValidateNonNegativeOffset(layout.StepCurrentTimeOffset, LayoutName, nameof(layout.StepCurrentTimeOffset), validationResults);
		ValidateNonNegativeOffset(layout.ForLoopCount1Offset, LayoutName, nameof(layout.ForLoopCount1Offset), validationResults);
		ValidateNonNegativeOffset(layout.ForLoopCount2Offset, LayoutName, nameof(layout.ForLoopCount2Offset), validationResults);
		ValidateNonNegativeOffset(layout.ForLoopCount3Offset, LayoutName, nameof(layout.ForLoopCount3Offset), validationResults);
		ValidateNonNegativeOffset(layout.TotalSize, LayoutName, nameof(layout.TotalSize), validationResults);

		ValidateOffsetFits(layout.TotalSize, layout.RecipeActiveOffset, 2, LayoutName, nameof(layout.RecipeActiveOffset), validationResults);
		ValidateOffsetFits(layout.TotalSize, layout.ActualLineOffset, sizeof(int), LayoutName, nameof(layout.ActualLineOffset), validationResults);
		ValidateOffsetFits(layout.TotalSize, layout.StepCurrentTimeOffset, sizeof(int), LayoutName, nameof(layout.StepCurrentTimeOffset), validationResults);
		ValidateOffsetFits(layout.TotalSize, layout.ForLoopCount1Offset, sizeof(int), LayoutName, nameof(layout.ForLoopCount1Offset), validationResults);
		ValidateOffsetFits(layout.TotalSize, layout.ForLoopCount2Offset, sizeof(int), LayoutName, nameof(layout.ForLoopCount2Offset), validationResults);
		ValidateOffsetFits(layout.TotalSize, layout.ForLoopCount3Offset, sizeof(int), LayoutName, nameof(layout.ForLoopCount3Offset), validationResults);
	}

	private static void ValidateNonNegativeOffset(
		int value,
		string layoutName,
		string fieldName,
		List<Result> validationResults)
	{
		if (value < 0)
		{
			validationResults.Add(Result.Fail(
				$"{layoutName}.{fieldName} ({value}) must be non-negative"));
		}
	}

	private static void ValidateOffsetFits(
		int totalSize,
		int offset,
		int fieldSize,
		string layoutName,
		string offsetFieldName,
		List<Result> validationResults)
	{
		if (totalSize < offset + fieldSize)
		{
			validationResults.Add(Result.Fail(
				$"{layoutName}.{nameof(ExecutionDbLayout.TotalSize)} ({totalSize}) must be at least " +
				$"{offsetFieldName} ({offset}) + {fieldSize} bytes"));
		}
	}
}
