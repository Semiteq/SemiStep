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

	[Theory]
	[InlineData(PlcSyncStatus.Idle, "Idle")]
	[InlineData(PlcSyncStatus.Syncing, "Syncing...")]
	[InlineData(PlcSyncStatus.Synced, "Synced")]
	[InlineData(PlcSyncStatus.Failed, "Failed")]
	public void MapSyncStatus_UnderEnglishCulture_ReturnsNeutralText(PlcSyncStatus status, string expected)
	{
		WithCulture("en", () => MainWindowViewModel.MapSyncStatus(status).Should().Be(expected));
	}

	[Fact]
	public void MapSyncStatus_OutOfSync_ReturnsEmptyRegardlessOfCulture()
	{
		WithCulture("ru", () => MainWindowViewModel.MapSyncStatus(PlcSyncStatus.OutOfSync).Should().BeEmpty());
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

	[Theory]
	[InlineData(0, "Ошибок: 0")]
	[InlineData(1, "Ошибок: 1")]
	[InlineData(5, "Ошибок: 5")]
	public void FormatErrorCount_UnderRussianCulture_ReturnsRussianText(int count, string expected)
	{
		WithCulture("ru", () => MessagePanelViewModel.FormatErrorCount(count).Should().Be(expected));
	}

	[Theory]
	[InlineData(0, "Предупреждений: 0")]
	[InlineData(1, "Предупреждений: 1")]
	[InlineData(5, "Предупреждений: 5")]
	public void FormatWarningCount_UnderRussianCulture_ReturnsRussianText(int count, string expected)
	{
		WithCulture("ru", () => MessagePanelViewModel.FormatWarningCount(count).Should().Be(expected));
	}

	[Fact]
	public void FormatErrorCount_UnderEnglishCulture_ReturnsNeutralText()
	{
		WithCulture("en", () => MessagePanelViewModel.FormatErrorCount(2).Should().Be("Errors: 2"));
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
