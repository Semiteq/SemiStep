using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="LoopPalette"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class LoopPaletteDraft(LoopPalette source)
{
	public ColorDraft Color { get; } = new(source.Color);

	public NumberDraft Depth1Weight { get; } =
		new((decimal)source.Depth1Weight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public NumberDraft Depth2Weight { get; } =
		new((decimal)source.Depth2Weight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public NumberDraft Depth3Weight { get; } =
		new((decimal)source.Depth3Weight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public LoopPalette Build()
	{
		return new LoopPalette(
			Color: Color.ToStyleColor(),
			Depth1Weight: Depth1Weight.ToDouble(),
			Depth2Weight: Depth2Weight.ToDouble(),
			Depth3Weight: Depth3Weight.ToDouble());
	}
}
