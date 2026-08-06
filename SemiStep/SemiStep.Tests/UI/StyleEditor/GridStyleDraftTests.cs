using System;

using FluentAssertions;

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
	public void DepthPaletteDraft_RoundTrips_FromReadOnlyCellsFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().ReadOnlyCells;

		new DepthPaletteDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void DepthPaletteDraft_RoundTrips_FromDisabledCellsFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().DisabledCells;

		new DepthPaletteDraft(fixture).Build().Should().Be(fixture);
	}

	[Fact]
	public void ExecutionPaletteDraft_RoundTrips_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct().Execution;

		new ExecutionPaletteDraft(fixture).Build().Should().Be(fixture);
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
	public void DraftNumbers_ToInt_Throws_OnNull()
	{
		var toInt = () => DraftNumbers.ToInt(null);

		toInt.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void DraftNumbers_ToDouble_Throws_OnNull()
	{
		var toDouble = () => DraftNumbers.ToDouble(null);

		toDouble.Should().Throw<InvalidOperationException>();
	}
}
