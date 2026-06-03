using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class ResultReportingExtensionsTests : IDisposable
{
	private readonly MessagePanelViewModel _panel = new();

	public void Dispose()
	{
		_panel.Dispose();
	}

	[Fact]
	public void FormatErrors_SuccessResult_ReturnsEmptyString()
	{
		var result = Result.Ok();

		result.FormatErrors().Should().BeEmpty();
	}

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
	public void ReportFailure_SingleErrorWithoutContext_SurfacesThatMessage()
	{
		var result = Result.Fail("only error");

		_panel.ReportFailure(result);

		var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Be("only error");
	}

	[AvaloniaFact]
	public void ReportFailure_WithoutContext_SurfacesAllErrorMessages()
	{
		var result = Result.Fail("first").WithError("second");

		_panel.ReportFailure(result);

		var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Be("first; second");
	}

	[AvaloniaFact]
	public void ReportFailure_WithContext_PrefixesAndSurfacesAllErrorMessages()
	{
		var result = Result.Fail("first").WithError("second");

		_panel.ReportFailure(result, "Step 1");

		var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Be("Step 1: first; second");
	}
}
