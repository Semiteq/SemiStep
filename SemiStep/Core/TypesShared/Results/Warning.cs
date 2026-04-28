using FluentResults;

namespace TypesShared.Results;

public sealed class Warning(string message) : Success(message);
