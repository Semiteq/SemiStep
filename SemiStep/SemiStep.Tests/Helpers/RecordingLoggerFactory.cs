using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace SemiStep.Tests.Helpers;

public sealed class RecordingLoggerFactory : ILoggerFactory
{
	private readonly ConcurrentDictionary<string, RecordingLogger<RecordingLoggerFactory>> _loggers = new();

	public IReadOnlyList<RecordingLogger<RecordingLoggerFactory>.LogEntry> Snapshot<TCategory>()
	{
		var category = typeof(TCategory).FullName!;

		return _loggers.TryGetValue(category, out var logger) ? logger.Snapshot() : [];
	}

	public ILogger CreateLogger(string categoryName)
	{
		return _loggers.GetOrAdd(categoryName, _ => new RecordingLogger<RecordingLoggerFactory>());
	}

	public void AddProvider(ILoggerProvider provider)
	{
	}

	public void Dispose()
	{
	}
}
