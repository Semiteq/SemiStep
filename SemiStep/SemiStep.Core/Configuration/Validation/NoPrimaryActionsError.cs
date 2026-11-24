using FluentResults;

namespace SemiStep.Core.Configuration.Validation;

internal sealed class NoPrimaryActionsError()
	: Error("No primary action is defined; the configuration must define at least one action with role 'action'");
