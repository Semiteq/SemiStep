using System.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.Csv.Helpers;
using SemiStep.Tests.UI.Localization;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.Csv.Integration;

[Trait("Category", "Integration")]
[Trait("Component", "Csv")]
[Trait("Area", "Localization")]
public sealed class CsvImportLocalizationTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	private const string OutOfRangeCsv = "action;step_duration;task;comment\n10;100000;0;ok\n";

	private const string RussianChain =
		"Строка 2: Столбец «step_duration»: Значение 100000 больше максимума 86400 для «time»";

	[Fact]
	public void Deserialize_OutOfRangeCell_LocalizesFullRussianChain()
	{
		var result = fixture.FileSerializer.Deserialize(OutOfRangeCsv);

		result.IsFailed.Should().BeTrue();

		using (ResourcesCultureScope.Use("ru"))
		{
			result.Errors.Select(ReasonLocalizer.Localize)
				.Should().Contain(RussianChain,
					"the AtRow -> AtColumn -> typed value error composition must render Russian end to end");
		}
	}

	[AvaloniaFact]
	public void Deserialize_OutOfRangeCell_SurfacedViaPanel_UnderRussianCulture_ShowsRussianChain()
	{
		using var panel = new MessagePanelViewModel();
		var result = fixture.FileSerializer.Deserialize(OutOfRangeCsv);

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.ReportFailure(result);
		}

		var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Contain(RussianChain);
	}
}
