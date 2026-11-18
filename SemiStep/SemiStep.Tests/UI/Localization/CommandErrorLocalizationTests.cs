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
	public async Task ReportThrownExceptions_UnderRussianCulture_PanelUsesLocalizedContextAndLogUsesInvariantContext()
	{
		using var panel = new MessagePanelViewModel();
		var logger = new RecordingLogger<object>();
		var failure = new InvalidOperationException("boom");
		using var command = ReactiveCommand.Create<Unit, Unit>(_ => throw failure);
		var context = new LocalizedText(nameof(Resources.CopyStepFailed));

		using (ResourcesCultureScope.Use("ru"))
		using (command.ReportThrownExceptions(panel, logger, context))
		{
			var execute = async () => await command.Execute();
			await execute.Should().ThrowAsync<InvalidOperationException>();

			var entry = panel.Entries.Should().ContainSingle().Subject;
			entry.Severity.Should().Be(MessageSeverity.Error);
			entry.Message.Should().StartWith(context.Localized);

			var logged = logger.Entries.Should().ContainSingle().Subject;
			logged.Level.Should().Be(LogLevel.Error);
			logged.Exception.Should().BeSameAs(failure);
			logged.Message.Should().Be(context.Invariant);

			context.Localized.Should().NotBe(context.Invariant);
		}
	}
}
