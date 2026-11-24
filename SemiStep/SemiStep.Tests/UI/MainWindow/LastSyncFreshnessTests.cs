using System.Globalization;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.Localization;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "LastSyncFreshness")]
[Trait("Category", "Unit")]
public sealed class LastSyncFreshnessTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;

	public static TheoryData<string, double, Func<string>, int?> AgeCases { get; } = new()
	{
		{ "en", 0, () => Resources.LastSyncAgoFormat, 0 },
		{ "en", 12.7, () => Resources.LastSyncAgoFormat, 12 },
		{ "en", 29.9, () => Resources.LastSyncAgoFormat, 29 },
		{ "en", 30, () => Resources.LastSyncOver30SecondsAgo, null },
		{ "en", 59.9, () => Resources.LastSyncOver30SecondsAgo, null },
		{ "en", 60, () => Resources.LastSyncOverMinuteAgo, null },
		{ "en", 3599.9, () => Resources.LastSyncOverMinuteAgo, null },
		{ "en", 3600, () => Resources.LastSyncOverHourAgo, null },
		{ "en", 86400, () => Resources.LastSyncOverHourAgo, null },
		{ "en", -5, () => Resources.LastSyncAgoFormat, 0 },
		{ "ru", 0, () => Resources.LastSyncAgoFormat, 0 },
		{ "ru", 12.7, () => Resources.LastSyncAgoFormat, 12 },
		{ "ru", 29.9, () => Resources.LastSyncAgoFormat, 29 },
		{ "ru", 30, () => Resources.LastSyncOver30SecondsAgo, null },
		{ "ru", 59.9, () => Resources.LastSyncOver30SecondsAgo, null },
		{ "ru", 60, () => Resources.LastSyncOverMinuteAgo, null },
		{ "ru", 3599.9, () => Resources.LastSyncOverMinuteAgo, null },
		{ "ru", 3600, () => Resources.LastSyncOverHourAgo, null },
		{ "ru", 86400, () => Resources.LastSyncOverHourAgo, null },
		{ "ru", -5, () => Resources.LastSyncAgoFormat, 0 },
	};

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[MemberData(nameof(AgeCases))]
	public void LastSyncTimeText_ShowsTheBucketOfTheAge(
		string culture,
		double ageSeconds,
		Func<string> bucketText,
		int? shownSeconds)
	{
		using (ResourcesCultureScope.Use(culture))
		{
			ObserveSyncAged(TimeSpan.FromSeconds(ageSeconds));

			var bucket = bucketText();
			var value = shownSeconds is null
				? bucket
				: string.Format(CultureInfo.InvariantCulture, bucket, shownSeconds);

			_viewModel.LastSyncTimeText.Should()
				.Be(string.Format(CultureInfo.InvariantCulture, Resources.LastSyncPrefix, value));
		}
	}

	[AvaloniaTheory]
	[InlineData("en")]
	[InlineData("ru")]
	public void LastSyncTimeText_WithoutSync_ShowsNever(string culture)
	{
		using (ResourcesCultureScope.Use(culture))
		{
			_viewModel.LastSyncTimeText.Should()
				.Be(string.Format(CultureInfo.InvariantCulture, Resources.LastSyncPrefix, Resources.LastSyncNever));
		}
	}

	private void ObserveSyncAged(TimeSpan age)
	{
		_fixture.PlcSyncService.LastSyncTime = _fixture.Scheduler.Now - age;
	}
}
