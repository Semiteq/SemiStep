using FluentResults;

namespace SemiStep.Tests.Helpers;

internal static class ResultAssertions
{
	public static T EnsureSuccess<T>(this Result<T> result, string operation)
	{
		if (result.IsFailed)
		{
			throw new InvalidOperationException(
				$"{operation} failed: {string.Join("; ", result.Errors.Select(e => e.Message))}");
		}

		return result.Value;
	}

	public static void EnsureSuccess(this Result result, string operation)
	{
		if (result.IsFailed)
		{
			throw new InvalidOperationException(
				$"{operation} failed: {string.Join("; ", result.Errors.Select(e => e.Message))}");
		}
	}
}
