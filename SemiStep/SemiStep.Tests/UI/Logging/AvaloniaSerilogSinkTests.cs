using FluentAssertions;

using SemiStep.UI.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Xunit;

using AvaloniaLevel = Avalonia.Logging.LogEventLevel;

namespace SemiStep.Tests.UI.Logging;

[Trait("Component", "UI")]
[Trait("Area", "Logging")]
[Trait("Category", "Unit")]
public sealed class AvaloniaSerilogSinkTests
{
	[Fact]
	public void IdenticalEvents_LogFullThenSampled_CarryingRunningCount()
	{
		var events = Capture(sink =>
		{
			for (var i = 0; i < 1000; i++)
			{
				sink.Log(AvaloniaLevel.Warning, "Binding", null, "Ancestor not found {Path}", "P");
			}
		});

		// First 20 in full, then every 500th (occurrence 500 and 1000).
		events.Should().HaveCount(22);
		events.Take(20).Should().OnlyContain(e => !e.Properties.ContainsKey("Occurrence"));
		OccurrenceOf(events[20]).Should().Be(500);
		OccurrenceOf(events[21]).Should().Be(1000);
	}

	[Fact]
	public void DistinctKeys_AreThrottledIndependently()
	{
		var events = Capture(sink =>
		{
			for (var i = 0; i < 21; i++)
			{
				sink.Log(AvaloniaLevel.Warning, "AreaA", null, "Same template {X}", i);
				sink.Log(AvaloniaLevel.Warning, "AreaB", null, "Same template {X}", i);
			}
		});

		// Each (area+template) key emits its own first 20; the 21st of each is dropped.
		events.Should().HaveCount(40);
		events.Count(e => Rendered(e, "SourceContext") == "Avalonia.AreaA").Should().Be(20);
		events.Count(e => Rendered(e, "SourceContext") == "Avalonia.AreaB").Should().Be(20);
	}

	[Theory]
	[InlineData(AvaloniaLevel.Verbose, LogEventLevel.Verbose)]
	[InlineData(AvaloniaLevel.Debug, LogEventLevel.Debug)]
	[InlineData(AvaloniaLevel.Information, LogEventLevel.Information)]
	[InlineData(AvaloniaLevel.Warning, LogEventLevel.Warning)]
	[InlineData(AvaloniaLevel.Error, LogEventLevel.Error)]
	[InlineData(AvaloniaLevel.Fatal, LogEventLevel.Fatal)]
	public void EachLevel_MapsToExpectedSerilogLevel(AvaloniaLevel avaloniaLevel, LogEventLevel expected)
	{
		var events = Capture(
			sink => sink.Log(avaloniaLevel, "Layout", null, "message"),
			minimumLevel: AvaloniaLevel.Verbose);

		events.Should().ContainSingle();
		events[0].Level.Should().Be(expected);
	}

	[Fact]
	public void TemplateAndArgs_ReachSerilogStructured()
	{
		var events = Capture(sink =>
			sink.Log(AvaloniaLevel.Warning, "Binding", null, "Value {Count} at {Name}", 42, "foo"));

		events.Should().ContainSingle();
		var logEvent = events[0];
		logEvent.MessageTemplate.Text.Should().Be("Value {Count} at {Name}");
		ScalarValueOf(logEvent, "Count").Should().Be(42);
		ScalarValueOf(logEvent, "Name").Should().Be("foo");
		ScalarValueOf(logEvent, "SourceContext").Should().Be("Avalonia.Binding");
	}

	private static IReadOnlyList<LogEvent> Capture(
		Action<AvaloniaSerilogSink> feed,
		AvaloniaLevel minimumLevel = AvaloniaLevel.Warning)
	{
		var capturingSink = new CapturingSink();
		var logger = new LoggerConfiguration()
			.MinimumLevel.Verbose()
			.WriteTo.Sink(capturingSink)
			.CreateLogger();

		var previous = Log.Logger;
		Log.Logger = logger;
		try
		{
			feed(new AvaloniaSerilogSink(minimumLevel));
		}
		finally
		{
			Log.Logger = previous;
			logger.Dispose();
		}

		return capturingSink.Events;
	}

	private static int OccurrenceOf(LogEvent logEvent)
	{
		return (int)((ScalarValue)logEvent.Properties["Occurrence"]).Value!;
	}

	private static object? ScalarValueOf(LogEvent logEvent, string property)
	{
		return ((ScalarValue)logEvent.Properties[property]).Value;
	}

	private static string? Rendered(LogEvent logEvent, string property)
	{
		return ScalarValueOf(logEvent, property) as string;
	}

	private sealed class CapturingSink : ILogEventSink
	{
		private readonly List<LogEvent> _events = new();

		public IReadOnlyList<LogEvent> Events => _events;

		public void Emit(LogEvent logEvent)
		{
			_events.Add(logEvent);
		}
	}
}
