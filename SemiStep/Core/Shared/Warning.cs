using FluentResults;

namespace SemiStep.Core.Shared;

public sealed class Warning(string message) : Success(message);
