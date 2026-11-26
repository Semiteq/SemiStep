using AwesomeAssertions;

using SemiStep.Core.Configuration;

using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.Styles;

[Trait("Component", "UI")]
[Trait("Area", "GridPalette")]
[Trait("Category", "Unit")]
public sealed class CellPaletteTests
{
	private static readonly CellPalette _palette = CellPalette.Build(GridStyleOptions.Default);

	// The approved table from the UI design: base, loop depth, then idle / past / running and the same three
	// selected. The unreachable orange past and running entries are pinned too.
	public static TheoryData<string, int, string, string, string, string, string, string> ApprovedTable => new()
	{
		{ "Plain", 0, "#FFFFFF", "#E4F1E8", "#BCDCC5", "#CDDCFB", "#B8D2EA", "#9AC2D0" },
		{ "Plain", 1, "#F5F1FB", "#DBE5E4", "#B5D2C2", "#C5D2F8", "#B2C9E7", "#95BBCE" },
		{ "Plain", 2, "#EAE3F6", "#D2D8E0", "#ADC8BE", "#BDC7F5", "#ABBFE4", "#8FB3CB" },
		{ "Plain", 3, "#DDD1F1", "#C6C8DB", "#A4BCBB", "#B3BAF1", "#A2B3E0", "#88AAC8" },
		{ "Inapplicable", 0, "#E0E0E0", "#C9D6CC", "#A6C6AF", "#B5C5E4", "#A4BED5", "#8AB2BF" },
		{ "Inapplicable", 1, "#D8D4DE", "#C2CBCB", "#A1BEAD", "#AFBCE3", "#9FB5D4", "#86ACBE" },
		{ "Inapplicable", 2, "#D0C8DC", "#BBC1C9", "#9BB5AC", "#A9B3E1", "#9AAED3", "#82A5BD" },
		{ "Inapplicable", 3, "#C6BADA", "#B2B4C7", "#94ACAB", "#A2A9E0", "#93A4D1", "#7C9EBC" },
		{ "ReadOnly", 0, "#D8D8D8", "#C2CFC5", "#A1C1A9", "#AFBFDE", "#9FB8D0", "#86AEBB" },
		{ "ReadOnly", 1, "#D1CDD7", "#BCC5C4", "#9CB9A9", "#AAB7DD", "#9AB1CF", "#82A8BB" },
		{ "ReadOnly", 2, "#C9C2D5", "#B5BBC3", "#96B1A7", "#A4AFDC", "#95A9CE", "#7EA2B9" },
		{ "ReadOnly", 3, "#C0B4D4", "#ADAFC2", "#90A7A6", "#9DA4DB", "#8FA0CE", "#799AB9" },
		{ "Changed", 0, "#FFCC80", "#E4C478", "#BCB86C", "#CDB69C", "#B8B096", "#9AA78D" },
		{ "Changed", 1, "#F5C286", "#DBBB7D", "#B5B170", "#C5AFA1", "#B2A99A", "#95A290" },
		{ "Changed", 2, "#EAB88C", "#D2B282", "#ADAA74", "#BDA7A5", "#ABA39E", "#8F9D93" },
		{ "Changed", 3, "#DDAB93", "#C6A789", "#A4A179", "#B39DAA", "#A29AA3", "#889697" },
	};

	[Theory]
	[MemberData(nameof(ApprovedTable))]
	public void Build_EveryEntryMatchesTheApprovedTable(
		string cellBase,
		int depth,
		string idle,
		string past,
		string running,
		string selectedIdle,
		string selectedPast,
		string selectedRunning)
	{
		var kind = Enum.Parse<CellBase>(cellBase);

		HexOf(kind, depth, CellExecutionState.Idle, selected: false).Should().Be(idle);
		HexOf(kind, depth, CellExecutionState.Past, selected: false).Should().Be(past);
		HexOf(kind, depth, CellExecutionState.Running, selected: false).Should().Be(running);
		HexOf(kind, depth, CellExecutionState.Idle, selected: true).Should().Be(selectedIdle);
		HexOf(kind, depth, CellExecutionState.Past, selected: true).Should().Be(selectedPast);
		HexOf(kind, depth, CellExecutionState.Running, selected: true).Should().Be(selectedRunning);
	}

	[Fact]
	public void Resolve_DepthAboveThree_UsesDepthThree()
	{
		_palette.Resolve(CellBase.Plain, 4, CellExecutionState.Past, selected: true)
			.Should().BeSameAs(_palette.Resolve(CellBase.Plain, 3, CellExecutionState.Past, selected: true));
	}

	[Fact]
	public void BaseOf_OrangeWinsOverGreyAndGreyOverPlain()
	{
		CellPalette.BaseOf(isChanged: true, isInapplicable: true, isReadOnly: true).Should().Be(CellBase.Changed);
		CellPalette.BaseOf(isChanged: false, isInapplicable: true, isReadOnly: true).Should().Be(CellBase.Inapplicable);
		CellPalette.BaseOf(isChanged: false, isInapplicable: false, isReadOnly: true).Should().Be(CellBase.ReadOnly);
		CellPalette.BaseOf(isChanged: false, isInapplicable: false, isReadOnly: false).Should().Be(CellBase.Plain);
	}

	[Fact]
	public void Blend_RoundsHalfUpPerChannel()
	{
		// 255 * 0.7 + 32 * 0.3 = 188.1 rounds to 188; 0 * 0.5 + 1 * 0.5 = 0.5 rounds up to 1.
		CellPalette.Blend(StyleColor.Parse("#FFFFFF"), StyleColor.Parse("#208A3C"), 300)
			.Should().Be(StyleColor.Parse("#BCDCC5"));
		CellPalette.Blend(StyleColor.Parse("#000000"), StyleColor.Parse("#010101"), 500)
			.Should().Be(StyleColor.Parse("#010101"));
	}

	private static string HexOf(CellBase cellBase, int depth, CellExecutionState state, bool selected)
	{
		var color = _palette.Resolve(cellBase, depth, state, selected).Color;
		return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}
}
