using FluentResults;

namespace Shared.Results;

public sealed class Warning(string message) : IReason
{
	public string Message { get; } = message;
	public Dictionary<string, object> Metadata { get; } = [];
}
