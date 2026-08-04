using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleOrientationInvalidError(string value, string expectedRows, string expectedColumns)
	: Error($"Grid style 'orientation' has unknown value: '{value}'. Expected '{expectedRows}' or '{expectedColumns}'.")
{
	public string Value { get; } = value;

	public string ExpectedRows { get; } = expectedRows;

	public string ExpectedColumns { get; } = expectedColumns;
}
