using FluentResults;

namespace Shared.Results;

public static class ResultWarningExtensions
{
	extension<T>(Result<T> result)
	{
		public Result<T> WithWarning(string message)
		{
			return result.WithReason(new Warning(message));
		}

		public Result<T> WithWarnings(IEnumerable<string> messages)
		{
			foreach (var message in messages)
			{
				result = result.WithReason(new Warning(message));
			}

			return result;
		}

		public IReadOnlyList<string> GetWarnings()
		{
			return result.Reasons
				.OfType<Warning>()
				.Select(w => w.Message)
				.ToList();
		}

		public bool HasWarnings()
		{
			return result.Reasons.OfType<Warning>().Any();
		}
	}
}
