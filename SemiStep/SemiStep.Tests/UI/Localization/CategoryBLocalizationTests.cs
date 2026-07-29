using System.Globalization;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class CategoryBLocalizationTests
{
	// PasteStepFailed is a localized context, so the panel entry opens with the Russian prefix.
	[AvaloniaFact]
	public void PasteSinkContext_UnderRussianCulture_PrefixesPanelWithRussianText()
	{
		using var panel = new MessagePanelViewModel();
		var result = Result.Fail("bad clipboard");

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.ReportFailure(result, Resources.PasteStepFailed);
		}

		var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().StartWith("Не удалось вставить:");
	}

	// StepActionChangeFailedFormat is a localized context, so the panel entry opens with the Russian prefix.
	[AvaloniaFact]
	public void ChangeActionSinkContext_UnderRussianCulture_PrefixesPanelWithRussianText()
	{
		using var panel = new MessagePanelViewModel();
		var result = Result.Fail("unknown action");

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.ReportFailure(
				result,
				string.Format(CultureInfo.InvariantCulture, Resources.StepActionChangeFailedFormat, 1));
		}

		var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().StartWith("Шаг 1: не удалось изменить действие");
	}
}
