using FluentResults;

namespace SemiStep.Core.Configuration.Validation;

internal sealed class StringMaxLengthConflictError(IReadOnlyList<int> maxLengths)
	: Error($"String properties disagree on max_length ({string.Join(", ", maxLengths)}); "
		+ "all system_type 'string' properties must share one max_length")
{
	public IReadOnlyList<int> MaxLengths { get; } = maxLengths;
}
