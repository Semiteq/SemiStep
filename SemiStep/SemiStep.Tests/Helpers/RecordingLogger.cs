using Microsoft.Extensions.Logging;

namespace SemiStep.Tests.Helpers;

public sealed class RecordingLogger<T> : ILogger<T>
{
	private readonly List<LogEntry> _entries = [];
	private readonly Lock _entriesLock = new();

	/// <summary>
	/// Producers may log from a thread-pool thread while the test thread reads, so the copy is taken
	/// under the same lock as the append.
	/// </summary>
	public IReadOnlyList<LogEntry> Snapshot()
	{
		lock (_entriesLock)
		{
			return [.. _entries];
		}
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return true;
	}

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var entry = new LogEntry(logLevel, formatter(state, exception), exception, CaptureProperties(state));

		lock (_entriesLock)
		{
			_entries.Add(entry);
		}
	}

	private static Dictionary<string, object?> CaptureProperties<TState>(TState state)
	{
		return state is IReadOnlyList<KeyValuePair<string, object?>> templateValues
			? templateValues.ToDictionary(pair => pair.Key, pair => pair.Value)
			: [];
	}

	/// <summary>A recorded call; <see cref="Properties"/> holds the template placeholder values unformatted.</summary>
	public sealed record LogEntry(
		LogLevel Level,
		string Message,
		Exception? Exception,
		IReadOnlyDictionary<string, object?> Properties);

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
		}
	}
}
