using FluentResults;

using SemiStep.Core.Plc.Configuration.Memory;

namespace SemiStep.Core.Plc.Configuration;

internal static class PlcConfigurationValidator
{
	// S7 BOOL pair (RecipeActive uses two adjacent bool bytes).
	private const int RecipeActiveSize = 2;

	public static Result Validate(PlcConfiguration config)
	{
		var validationResults = new List<Result>();

		ValidateManagingDb(config.Layout.ManagingDb, validationResults);
		ValidateDataDb(config.Layout.IntDb, nameof(PlcProtocolLayout.IntDb), validationResults);
		ValidateDataDb(config.Layout.FloatDb, nameof(PlcProtocolLayout.FloatDb), validationResults);
		ValidateDataDb(config.Layout.StringDb, nameof(PlcProtocolLayout.StringDb), validationResults);
		ValidateExecutionDb(config.Layout.ExecutionDb, validationResults);

		return Result.Merge(validationResults.ToArray());
	}

	private static void ValidateManagingDb(ManagingDbLayout layout, List<Result> validationResults)
	{
		const string LayoutName = nameof(ManagingDbLayout);

		ValidateNonNegativeOffset(layout.CommittedOffset, LayoutName, nameof(layout.CommittedOffset), validationResults);
		ValidateNonNegativeOffset(layout.RecipeLinesOffset, LayoutName, nameof(layout.RecipeLinesOffset), validationResults);
		ValidateNonNegativeOffset(layout.TotalSize, LayoutName, nameof(layout.TotalSize), validationResults);

		if (layout.TotalSize < layout.RecipeLinesOffset + sizeof(int))
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

		ValidateNoOverlap(
			LayoutName,
			(nameof(layout.CommittedOffset), layout.CommittedOffset, sizeof(byte)),
			(nameof(layout.RecipeLinesOffset), layout.RecipeLinesOffset, sizeof(int)),
			validationResults);
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

		ValidateNoOverlap(
			layoutName,
			(nameof(layout.CapacityOffset), layout.CapacityOffset, sizeof(int)),
			(nameof(layout.CurrentSizeOffset), layout.CurrentSizeOffset, sizeof(int)),
			validationResults);
	}

	private static void ValidateExecutionDb(ExecutionDbLayout layout, List<Result> validationResults)
	{
		const string LayoutName = nameof(ExecutionDbLayout);

		var fields = new (string Name, int Offset, int Size)[]
		{
			(nameof(layout.RecipeActiveOffset), layout.RecipeActiveOffset, RecipeActiveSize),
			(nameof(layout.ActualLineOffset), layout.ActualLineOffset, sizeof(int)),
			(nameof(layout.StepCurrentTimeOffset), layout.StepCurrentTimeOffset, sizeof(int)),
			(nameof(layout.ForLoopCount1Offset), layout.ForLoopCount1Offset, sizeof(int)),
			(nameof(layout.ForLoopCount2Offset), layout.ForLoopCount2Offset, sizeof(int)),
			(nameof(layout.ForLoopCount3Offset), layout.ForLoopCount3Offset, sizeof(int)),
		};

		foreach (var field in fields)
		{
			ValidateNonNegativeOffset(field.Offset, LayoutName, field.Name, validationResults);
			ValidateOffsetFits(layout.TotalSize, field.Offset, field.Size, LayoutName, field.Name, validationResults);
		}

		ValidateNonNegativeOffset(layout.TotalSize, LayoutName, nameof(layout.TotalSize), validationResults);

		for (var firstIndex = 0; firstIndex < fields.Length; firstIndex++)
		{
			for (var secondIndex = firstIndex + 1; secondIndex < fields.Length; secondIndex++)
			{
				ValidateNoOverlap(LayoutName, fields[firstIndex], fields[secondIndex], validationResults);
			}
		}
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
				$"{layoutName}.TotalSize ({totalSize}) must be at least " +
				$"{offsetFieldName} ({offset}) + {fieldSize} bytes"));
		}
	}

	private static void ValidateNoOverlap(
		string layoutName,
		(string Name, int Offset, int Size) first,
		(string Name, int Offset, int Size) second,
		List<Result> validationResults)
	{
		// Negative offsets are reported separately by ValidateNonNegativeOffset.
		// Skip overlap analysis for invalid inputs to avoid duplicate noise.
		if (first.Offset < 0 || second.Offset < 0)
		{
			return;
		}

		var firstEnd = first.Offset + first.Size;
		var secondEnd = second.Offset + second.Size;
		var overlaps = first.Offset < secondEnd && second.Offset < firstEnd;

		if (overlaps)
		{
			validationResults.Add(Result.Fail(
				$"{layoutName}.{first.Name} ({first.Offset}, {first.Size} bytes) overlaps with " +
				$"{second.Name} ({second.Offset}, {second.Size} bytes)"));
		}
	}
}
