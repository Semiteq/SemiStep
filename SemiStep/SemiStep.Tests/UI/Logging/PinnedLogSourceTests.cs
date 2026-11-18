using System.Globalization;

using FluentAssertions;

using SemiStep.UI;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Xunit;

namespace SemiStep.Tests.UI.Logging;

/// <summary>Pins the Serilog wiring that makes the PLC reconciliation lines readable in the field.</summary>
[Trait("Component", "UI")]
[Trait("Area", "Logging")]
[Trait("Category", "Unit")]
public sealed class PinnedLogSourceTests
{
	private const string UnpinnedSource = "SemiStep.Core.Plc.S7.S7Service";

	[Fact]
	public void PinnedSource_NamesTheLifecycleManagerType()
	{
		Program.PinnedReconciliationSource.Should().Be("SemiStep.Core.Plc.PlcLifecycleManager",
			"the override key is the source context Serilog writes, so a rename or namespace move must "
			+ "be a visible decision here rather than a silent unpin");
	}

	[Fact]
	public void AtTheFieldDefault_PinnedSourceLogsInformation_WhileOtherSourcesDoNot()
	{
		var events = Capture(LogEventLevel.Warning, logger =>
		{
			Write(logger, Program.PinnedReconciliationSource, LogEventLevel.Information, "pinned");
			Write(logger, UnpinnedSource, LogEventLevel.Information, "unpinned");
		});

		Rendered(events).Should().Equal(["pinned"],
			"the pin must lift this one source over the Warning default without lowering the floor for "
			+ "everything else");
	}

	[Fact]
	public void AtDebug_PinnedSourceStillLogsDebug()
	{
		var events = Capture(LogEventLevel.Debug, logger =>
		{
			Write(logger, Program.PinnedReconciliationSource, LogEventLevel.Debug, "pinned");
			Write(logger, UnpinnedSource, LogEventLevel.Debug, "unpinned");
		});

		Rendered(events).Should().Equal(["pinned", "unpinned"],
			"an override applied unconditionally would raise this source's floor to Information and drop "
			+ "its Debug lines");
	}

	[Fact]
	public void AtTheFieldDefault_PinnedSourceStillLogsWarning()
	{
		var events = Capture(LogEventLevel.Warning, logger =>
			Write(logger, Program.PinnedReconciliationSource, LogEventLevel.Warning, "pinned"));

		Rendered(events).Should().Equal(["pinned"]);
	}

	private static void Write(ILogger logger, string sourceContext, LogEventLevel level, string message)
	{
		logger.ForContext(Constants.SourceContextPropertyName, sourceContext).Write(level, message);
	}

	private static IReadOnlyList<string> Rendered(IReadOnlyList<LogEvent> events)
	{
		return [.. events.Select(e => e.RenderMessage(CultureInfo.InvariantCulture))];
	}

	private static IReadOnlyList<LogEvent> Capture(LogEventLevel globalLevel, Action<ILogger> write)
	{
		var capturingSink = new CapturingSink();
		var logger = Program
			.ApplyMinimumLevels(new LoggerConfiguration(), globalLevel)
			.WriteTo.Sink(capturingSink)
			.CreateLogger();

		try
		{
			write(logger);
		}
		finally
		{
			logger.Dispose();
		}

		return capturingSink.Events;
	}

	private sealed class CapturingSink : ILogEventSink
	{
		private readonly List<LogEvent> _events = [];

		public IReadOnlyList<LogEvent> Events => _events;

		public void Emit(LogEvent logEvent)
		{
			_events.Add(logEvent);
		}
	}
}
