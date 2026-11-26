using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="LoopPalette"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class LoopPaletteDraft(LoopPalette source) : ReactiveObject
{
	public Color Color
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Color.ToMediaColor();

	public decimal? Depth1Weight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.Depth1Weight;

	public decimal? Depth2Weight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.Depth2Weight;

	public decimal? Depth3Weight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.Depth3Weight;

	public LoopPalette Build()
	{
		return new LoopPalette(
			Color: Color.ToStyleColor(),
			Depth1Weight: DraftNumbers.ToDouble(Depth1Weight),
			Depth2Weight: DraftNumbers.ToDouble(Depth2Weight),
			Depth3Weight: DraftNumbers.ToDouble(Depth3Weight));
	}
}
