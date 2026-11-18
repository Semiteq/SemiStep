using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Recipes.Analysis.Warnings;
using SemiStep.Core.Recipes.Formulas.Errors;

using SemiStep.Tests.UI.Localization;

using SemiStep.UI.Localization;
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

	[AvaloniaFact]
	public void ReportFailure_WithContext_PrefixesAndSurfacesAllErrorMessages()
	{
		const string Context = "context";
		const string FirstMessage = "first";
		const string SecondMessage = "second";
		var result = Result.Fail(FirstMessage).WithError(SecondMessage);

		_panel.ReportFailure(result, Context);

		var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().StartWith(Context + ":");
		entry.Message.Should().Contain(FirstMessage).And.Contain(SecondMessage);
	}

	[AvaloniaFact]
	public void ReportFailure_ResultWithoutContext_UnderRussianCulture_SurfacesLocalizedText()
	{
		var error = FormulaFailed();

		using (ResourcesCultureScope.Use("ru"))
		{
			_panel.ReportFailure(Result.Fail(error));

			var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
			entry.Message.Should().Be(ReasonLocalizer.Localize(error));
			entry.Message.Should().NotBe(error.Message);
		}
	}

	[AvaloniaFact]
	public void ReportFailure_SingleError_UnderRussianCulture_SurfacesLocalizedText()
	{
		var error = FormulaFailed();

		using (ResourcesCultureScope.Use("ru"))
		{
			_panel.ReportFailure(error);

			var entry = _panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
			entry.Message.Should().Be(ReasonLocalizer.Localize(error));
			entry.Message.Should().NotBe(error.Message);
		}
	}

	[AvaloniaFact]
	public void ReportWarnings_TypedWarning_UnderRussianCulture_SurfacesLocalizedText()
	{
		var warning = new UnclosedForLoopWarning(1);

		using (ResourcesCultureScope.Use("ru"))
		{
			_panel.ReportWarnings(Result.Ok().WithSuccess(warning));

			var entry = _panel.Entries.Should().ContainSingle(item => item.IsWarning).Subject;
			entry.Message.Should().Be(ReasonLocalizer.Localize(warning));
			entry.Message.Should().NotBe(warning.Message);
		}
	}

	[AvaloniaFact]
	public void FormatErrors_TypedError_UnderRussianCulture_StaysRawEnglish()
	{
		var error = new FormulaComputationFailedError("temp", new Error("min > max"));
		var result = Result.Fail(error);

		using (ResourcesCultureScope.Use("ru"))
		{
			result.FormatErrors().Should().Be(error.Message);
		}
	}

	private static FormulaComputationFailedError FormulaFailed()
	{
		return new FormulaComputationFailedError("temp", new Error("min > max"));
	}
}
