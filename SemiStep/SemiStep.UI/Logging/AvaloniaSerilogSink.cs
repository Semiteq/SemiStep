using System.Collections.Concurrent;

using Avalonia.Logging;

using AvaloniaLevel = Avalonia.Logging.LogEventLevel;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace SemiStep.UI.Logging;

public sealed class AvaloniaSerilogSink(AvaloniaLevel minimumLevel) : ILogSink
{
	private const int FullLogCount = 20;
	private const int SampleInterval = 500;

	private readonly AvaloniaLevel _minimumLevel = minimumLevel;

	// The key set is trusted finite: Avalonia log templates are constant strings from a fixed set of
	// call sites, so this never grows unbounded and needs no eviction.
	private readonly ConcurrentDictionary<(string Area, string Template), int> _occurrences = new();

	public bool IsEnabled(AvaloniaLevel level, string area)
	{
		return level >= _minimumLevel;
	}

	public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate)
	{
		Log(level, area, source, messageTemplate, []);
	}

	public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
	{
		if (level < _minimumLevel)
		{
			return;
		}

		var occurrence = _occurrences.AddOrUpdate((area, messageTemplate), 1, static (_, count) => count + 1);
		if (!ShouldEmit(occurrence))
		{
			return;
		}

		var logger = Serilog.Log.ForContext("SourceContext", "Avalonia." + area);
		if (occurrence > FullLogCount)
		{
			logger = logger.ForContext("Occurrence", occurrence);
		}

		logger.Write(MapLevel(level), messageTemplate, propertyValues);
	}

	private static bool ShouldEmit(int occurrence)
	{
		return occurrence <= FullLogCount || occurrence % SampleInterval == 0;
	}

	private static SerilogLevel MapLevel(AvaloniaLevel level)
	{
		return level switch
		{
			AvaloniaLevel.Verbose => SerilogLevel.Verbose,
			AvaloniaLevel.Debug => SerilogLevel.Debug,
			AvaloniaLevel.Information => SerilogLevel.Information,
			AvaloniaLevel.Warning => SerilogLevel.Warning,
			AvaloniaLevel.Error => SerilogLevel.Error,
			AvaloniaLevel.Fatal => SerilogLevel.Fatal,
			_ => SerilogLevel.Information,
		};
	}
}
