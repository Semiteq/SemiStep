using FluentResults;

namespace SemiStep.Core.Configuration.Validation;

internal sealed class NoStringPropertyError()
	: Error("No property with system_type 'string' is defined; string max_length cannot be resolved");
