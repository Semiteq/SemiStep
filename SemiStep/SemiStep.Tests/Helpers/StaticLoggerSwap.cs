using Serilog;
using Serilog.Core;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Swaps the static <see cref="Log.Logger"/>; safe only because the assembly disables test parallelization.
/// </summary>
public sealed class StaticLoggerSwap : IDisposable
{
	private readonly ILogger _previous;

	private StaticLoggerSwap(ILogger previous)
	{
		_previous = previous;
	}

	public static StaticLoggerSwap To(ILogEventSink sink)
	{
		var previous = Log.Logger;
		Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		return new StaticLoggerSwap(previous);
	}

	public void Dispose()
	{
		(Log.Logger as IDisposable)?.Dispose();
		Log.Logger = _previous;
	}
}
