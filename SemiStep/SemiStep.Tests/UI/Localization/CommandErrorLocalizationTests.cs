using System;
using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Tests.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class CommandErrorLocalizationTests
{
	[AvaloniaFact]
	public async Task ReportThrownExceptions_UnderRussianCulture_ReportsRussianPrefixButLogsEnglishContext()
	{
		var panel = new MessagePanelViewModel();
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");
		var command = ReactiveCommand.Create<Unit, Unit>(_ => throw failure);
		var context = new LocalizedText(nameof(Resources.CopyStepFailed));

		try
		{
			using (ResourcesCultureScope.Use("ru"))
			using (command.ReportThrownExceptions(panel, logger, context))
			{
				try
				{
					await command.Execute();
				}
				catch (InvalidOperationException)
				{
					// Routed to ThrownExceptions; awaiting also surfaces it here.
				}

				var entry = panel.Entries.Should().ContainSingle().Subject;
				entry.Severity.Should().Be(MessageSeverity.Error);
				entry.Message.Should().Be("Не удалось скопировать: boom");

				var logged = logger.Entries.Should().ContainSingle().Subject;
				logged.Level.Should().Be(LogLevel.Error);
				logged.Exception.Should().BeSameAs(failure);
				logged.Message.Should().Be("Copy failed");
			}
		}
		finally
		{
			command.Dispose();
			panel.Dispose();
		}
	}
}
