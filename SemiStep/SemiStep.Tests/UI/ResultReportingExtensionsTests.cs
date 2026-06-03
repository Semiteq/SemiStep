using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class ResultReportingExtensionsTests
{
	[Fact]
	public void FormatErrors_SingleError_ReturnsThatMessage()
	{
		var result = Result.Fail("only error");

		result.FormatErrors().Should().Be("only error");
	}

	[Fact]
	public void FormatErrors_MultipleErrors_JoinsAllMessagesInOrder()
	{
		var result = Result.Fail("first").WithError("second").WithError("third");

		result.FormatErrors().Should().Be("first; second; third");
	}

	[AvaloniaFact]
	public void ReportFailure_WithoutContext_SurfacesAllErrorMessages()
	{
		var panel = new MessagePanelViewModel();
		var result = Result.Fail("first").WithError("second");

		try
		{
			panel.ReportFailure(result);

			var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
			entry.Message.Should().Be("first; second");
		}
		finally
		{
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public void ReportFailure_WithContext_PrefixesAndSurfacesAllErrorMessages()
	{
		var panel = new MessagePanelViewModel();
		var result = Result.Fail("first").WithError("second");

		try
		{
			panel.ReportFailure(result, "Step 1");

			var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
			entry.Message.Should().Be("Step 1: first; second");
		}
		finally
		{
			panel.Dispose();
		}
	}
}
