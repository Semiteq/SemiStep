using System;
using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Tests.Helpers;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.MessageService;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class ReactiveCommandReportingExtensionsTests
{
	[AvaloniaFact]
	public async Task ReportThrownExceptions_ThrowingCommand_ReportsSinglePanelErrorAndLogsException()
	{
		var panel = new MessagePanelViewModel();
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");
		var command = ReactiveCommand.Create<Unit, Unit>(_ => throw failure);

		using var subscription = command.ReportThrownExceptions(panel, logger, "Copy failed");

		try
		{
			await ExecuteSwallowing(command);

			var entry = panel.Entries.Should().ContainSingle().Subject;
			entry.Severity.Should().Be(MessageSeverity.Error);
			entry.Message.Should().Be("Copy failed: boom");

			var logged = logger.Entries.Should().ContainSingle().Subject;
			logged.Level.Should().Be(LogLevel.Error);
			logged.Exception.Should().BeSameAs(failure);
		}
		finally
		{
			command.Dispose();
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task ReportThrownExceptions_NonThrowingCommand_ProducesNoPanelEntryAndNoLog()
	{
		var panel = new MessagePanelViewModel();
		var logger = new RecordingLogger<object>();
		var command = ReactiveCommand.Create<Unit, Unit>(_ => Unit.Default);

		using var subscription = command.ReportThrownExceptions(panel, logger, "Copy failed");

		try
		{
			await command.Execute();

			panel.Entries.Should().BeEmpty();
			logger.Entries.Should().BeEmpty();
		}
		finally
		{
			command.Dispose();
			panel.Dispose();
		}
	}

	private static async Task ExecuteSwallowing(ReactiveCommand<Unit, Unit> command)
	{
		try
		{
			await command.Execute();
		}
		catch (InvalidOperationException)
		{
			// The command routes the throw to ThrownExceptions; Execute also rethrows to the awaiter.
		}
	}
}
