using Avalonia.Media;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Helpers;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

/// <summary>
/// Per-draft round-trip guards. Each draft seeds from a group of the all-distinct fixture and rebuilds
/// it; record value equality over mutually-distinct, exactly-representable fixture values makes the
/// assertion field-exhaustive — a dropped, unseeded, or cross-wired leaf mismatches exactly one field.
/// Plain <see cref="FactAttribute"/>: the drafts touch no Avalonia services, only the <c>Color</c> struct,
/// so they run outside the headless dispatcher.
/// </summary>
[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class GridStyleDraftTests
{
	[Fact]
	public void GridStyleFontsDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Fonts;

		new GridStyleFontsDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void GridStyleLayoutDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Layout;

		new GridStyleLayoutDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void SelectionColorsDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Selection;

		new SelectionColorsDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void ChangedCellColorsDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().ChangedCells;

		new ChangedCellColorsDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void ExecutionPaletteDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Execution;

		new ExecutionPaletteDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void LoopPaletteDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Loop;

		new LoopPaletteDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void StatusBarStyleDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().StatusBar;

		new StatusBarStyleDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void ValidationPanelStyleDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().ValidationPanel;

		new ValidationPanelStyleDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void ChromeColorsDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Chrome;

		new ChromeColorsDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void NumberDraft_ToInt_Throws_WhenEmpty()
	{
		var draft = new NumberDraft(10, 0, 100) { Value = null };

		Func<int> toInt = draft.ToInt;

		toInt.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void NumberDraft_ToDouble_Throws_WhenEmpty()
	{
		var draft = new NumberDraft(10, 0, 100) { Value = null };

		Func<double> toDouble = draft.ToDouble;

		toDouble.Should().Throw<InvalidOperationException>();
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData(-1, false)]
	[InlineData(0, true)]
	[InlineData(100, true)]
	[InlineData(101, false)]
	public void NumberDraft_IsValid_OnlyInsideItsRange(int? value, bool expected)
	{
		var draft = new NumberDraft(10, 0, 100) { Value = value };

		draft.IsValid.Should().Be(expected);
	}

	[Fact]
	public void ColorDraft_Pick_DropsTheAlpha_SoTheFileGetsSixDigits()
	{
		var draft = new ColorDraft(StyleColor.Parse("#80A1B2C3"));

		draft.Pick(Color.FromArgb(0x40, 0x12, 0x34, 0xAB));

		draft.Color.Should().Be(Color.FromRgb(0x12, 0x34, 0xAB));
		draft.ToStyleColor().ToString().Should().Be("#1234AB");
	}

	[Fact]
	public void ColorDraft_WithoutAPick_KeepsATranslucentSeed()
	{
		new ColorDraft(StyleColor.Parse("#80A1B2C3")).ToStyleColor().Should().Be(StyleColor.Parse("#80A1B2C3"));
	}
}
