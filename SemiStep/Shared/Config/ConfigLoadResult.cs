namespace Shared.Config;

public sealed record ConfigLoadResult(
	AppConfiguration? Configuration,
	IReadOnlyList<string> Errors)
{
	public bool HasErrors => Errors.Count > 0;
	public bool IsSuccess => Configuration is not null && !HasErrors;

	public static ConfigLoadResult Success(AppConfiguration configuration)
	{
		return new ConfigLoadResult(configuration, []);
	}

	public static ConfigLoadResult Failure(IReadOnlyList<string> errors)
	{
		return new ConfigLoadResult(null, errors);
	}
}
