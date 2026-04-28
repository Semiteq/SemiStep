using FluentResults;

namespace TypesShared.Results;

public static class ResultWarningExtensions
{
	extension(Result result)
	{
		public Result WithWarning(string message)
		{
			return result.WithSuccess(new Warning(message));
		}
	}

	extension<T>(Result<T> result)
	{
		public Result<T> WithWarning(string message)
		{
			return result.WithSuccess(new Warning(message));
		}
	}
}
