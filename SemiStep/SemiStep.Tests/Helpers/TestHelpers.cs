using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SemiStep.Tests.Helpers;

public static class TestHelpers
{
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
	public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(20);

	public static Task WaitUntilAsync(
		Func<bool> predicate,
		CancellationToken cancellationToken = default,
		[CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
	{
		return WaitUntilAsync(predicate, DefaultTimeout, DefaultPollInterval, cancellationToken, predicateExpression);
	}

	public static async Task WaitUntilAsync(
		Func<bool> predicate,
		TimeSpan timeout,
		TimeSpan pollInterval,
		CancellationToken cancellationToken = default,
		[CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
	{
		var sw = Stopwatch.StartNew();
		while (sw.Elapsed < timeout)
		{
			if (predicate())
			{
				return;
			}

			await Task.Delay(pollInterval, cancellationToken);
		}
		// Final boundary check: Task.Delay may have pushed elapsed past timeout while predicate could have become true.
		if (predicate())
		{
			return;
		}
		throw new TimeoutException(
			$"Predicate did not become true within {timeout}: {predicateExpression}");
	}
}
