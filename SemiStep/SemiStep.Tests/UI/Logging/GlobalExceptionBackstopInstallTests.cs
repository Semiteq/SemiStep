using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.UI.Logging;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.Logging;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Integration")]
public sealed class GlobalExceptionBackstopInstallTests
{
	[AvaloniaFact]
	public void RecoverableExceptionHandler_RoutesUnhandledExceptionToPanel()
	{
		var services = new ServiceCollection();
		services.AddSingleton<MessagePanelViewModel>();
		services.AddLogging();
		var provider = services.BuildServiceProvider();

		var panel = provider.GetRequiredService<MessagePanelViewModel>();

		try
		{
			var handler = GlobalExceptionBackstop.CreateRecoverableExceptionHandler(provider);

			handler.OnNext(new Exception("boom"));

			var entry = panel.Entries.Should().ContainSingle().Subject;
			entry.Severity.Should().Be(MessageSeverity.Error);
			entry.Message.Should().Be(GlobalExceptionBackstop.RecoverableUserMessage);
		}
		finally
		{
			panel.Dispose();
			provider.Dispose();
		}
	}
}
