using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ExecutionPalette"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ExecutionPaletteDraft(ExecutionPalette source) : ReactiveObject
{
	public Color Running
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Running.ToMediaColor();

	public decimal? RunningWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.RunningWeight;

	public decimal? PastWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.PastWeight;

	public ExecutionPalette Build()
	{
		return new ExecutionPalette(
			Running: Running.ToStyleColor(),
			RunningWeight: DraftNumbers.ToDouble(RunningWeight),
			PastWeight: DraftNumbers.ToDouble(PastWeight));
	}
}
