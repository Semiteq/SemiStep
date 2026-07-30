using SemiStep.Core.Shared;

namespace SemiStep.Core.Recipes.Import.Warnings;

public sealed class RowCountMismatchWarning(string filePath, int metadataRows, int actualRows)
	: Warning($"Row count mismatch in '{filePath}': metadata says {metadataRows}, actual is {actualRows}")
{
	public string FilePath { get; } = filePath;

	public int MetadataRows { get; } = metadataRows;

	public int ActualRows { get; } = actualRows;
}
