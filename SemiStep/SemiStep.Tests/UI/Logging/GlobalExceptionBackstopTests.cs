using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Tests.Helpers;
using SemiStep.UI.Logging;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.Logging;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class GlobalExceptionBackstopTests
{
	[AvaloniaFact]
	public void ReportRecoverable_LogsExceptionAndReportsGenericPanelMessage()
	{
		var panel = new MessagePanelViewModel();
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");

		try
		{
			GlobalExceptionBackstop.ReportRecoverable(panel, logger, failure);

			var entry = panel.Entries.Should().ContainSingle().Subject;
			entry.Severity.Should().Be(MessageSeverity.Error);
			entry.Message.Should().Be(GlobalExceptionBackstop.RecoverableUserMessage);

			var logged = logger.Entries.Should().ContainSingle().Subject;
			logged.Level.Should().Be(LogLevel.Error);
			logged.Exception.Should().BeSameAs(failure);
		}
		finally
		{
			panel.Dispose();
		}
	}

	[Fact]
	public void LogUnobserved_LogsExceptionAtErrorLevel()
	{
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");

		GlobalExceptionBackstop.LogUnobserved(logger, failure);

		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[Fact]
	public void LogFatal_LogsCriticalWithException()
	{
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");

		GlobalExceptionBackstop.LogFatal(logger, failure);

		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Critical);
		logged.Exception.Should().BeSameAs(failure);
	}
}
