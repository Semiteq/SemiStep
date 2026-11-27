using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ExecutionPalette"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ExecutionPaletteDraft(ExecutionPalette source)
{
	public ColorDraft Running { get; } = new(source.Running);

	public NumberDraft RunningWeight { get; } =
		new((decimal)source.RunningWeight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public NumberDraft PastWeight { get; } =
		new((decimal)source.PastWeight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public ExecutionPalette Build()
	{
		return new ExecutionPalette(
			Running: Running.ToStyleColor(),
			RunningWeight: RunningWeight.ToDouble(),
			PastWeight: PastWeight.ToDouble());
	}
}
