using System;
using System.Globalization;

using FluentAssertions;

using SemiStep.Core.Plc.State;

using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class ViewModelLocalizationTests
{
	[Theory]
	[InlineData(PlcSyncStatus.Idle, "Ожидание")]
	[InlineData(PlcSyncStatus.Syncing, "Синхронизация…")]
	[InlineData(PlcSyncStatus.Synced, "Синхронизировано")]
	[InlineData(PlcSyncStatus.Failed, "Ошибка")]
	public void MapSyncStatus_UnderRussianCulture_ReturnsRussianText(PlcSyncStatus status, string expected)
	{
		WithCulture("ru", () => MainWindowViewModel.MapSyncStatus(status).Should().Be(expected));
	}

	[Fact]
	public void FormatLastSyncTime_Never_UnderRussianCulture_ReturnsRussianText()
	{
		WithCulture("ru", () => MainWindowViewModel.FormatLastSyncTime(null).Should().Be("Синхр.: Никогда"));
	}

	[Fact]
	public void FormatLastSyncTime_Never_UnderEnglishCulture_ReturnsNeutralText()
	{
		WithCulture("en", () => MainWindowViewModel.FormatLastSyncTime(null).Should().Be("Last sync: Never"));
	}

	[Fact]
	public void FormatLastSyncTime_Elapsed_KeepsNumberInvariant()
	{
		WithCulture("ru", () =>
		{
			var text = MainWindowViewModel.FormatLastSyncTime(DateTimeOffset.UtcNow.AddSeconds(-100));

			text.Should().StartWith("Синхр.: ");
			text.Should().Contain("с назад");
			text.Should().Contain("100.0");
			text.Should().NotContain(",");
		});
	}

	[Fact]
	public void LastSyncAgoFormat_UnderRussianCulture_RendersInvariantDecimal()
	{
		WithCulture("ru", () =>
			string.Format(
					CultureInfo.InvariantCulture,
					Resources.LastSyncAgoFormat ?? "{0}",
					(12.5).ToString("0.0", CultureInfo.InvariantCulture))
				.Should().Be("12.5 с назад"));
	}

	[Fact]
	public void FormatErrorCount_UnderRussianCulture_ReturnsRussianText()
	{
		WithCulture("ru", () => MessagePanelViewModel.FormatErrorCount(5).Should().Be("Ошибок: 5"));
	}

	[Fact]
	public void FormatWarningCount_UnderRussianCulture_ReturnsRussianText()
	{
		WithCulture("ru", () => MessagePanelViewModel.FormatWarningCount(5).Should().Be("Предупреждений: 5"));
	}

	[Fact]
	public void PlcConflictStepCounts_UnderRussianCulture_ReturnRussianText()
	{
		WithCulture("ru", () =>
		{
			var viewModel = new PlcConflictDialogViewModel(3, 7);

			viewModel.LocalStepCountText.Should().Be("Локально шагов: 3");
			viewModel.PlcStepCountText.Should().Be("Шагов в ПЛК: 7");
		});
	}

	[Fact]
	public void PlcConflictStepCounts_UnderEnglishCulture_ReturnNeutralText()
	{
		WithCulture("en", () =>
		{
			var viewModel = new PlcConflictDialogViewModel(3, 7);

			viewModel.LocalStepCountText.Should().Be("Local steps: 3");
			viewModel.PlcStepCountText.Should().Be("PLC steps: 7");
		});
	}

	private static void WithCulture(string culture, Action assertion)
	{
		using (ResourcesCultureScope.Use(culture))
		{
			assertion();
		}
	}
}
