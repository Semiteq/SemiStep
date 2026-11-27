using ReactiveUI;

namespace SemiStep.UI.StyleEditor;

/// <summary>One editable number with its allowed range; <see cref="Value"/> is null while the box is empty.</summary>
public sealed class NumberDraft(decimal seed, decimal minimum, decimal maximum) : ReactiveObject
{
	public decimal Minimum { get; } = minimum;

	public decimal Maximum { get; } = maximum;

	public decimal? Value
	{
		get;
		set
		{
			if (field == value)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(IsValid));
		}
	} = seed;

	public bool IsValid => Value is { } value && value >= Minimum && value <= Maximum;

	public double ToDouble()
	{
		return (double)Require();
	}

	public int ToInt()
	{
		return (int)Math.Round(Require(), MidpointRounding.AwayFromZero);
	}

	private decimal Require()
	{
		return Value ?? throw new InvalidOperationException(
			"Build was called on a draft with an empty number; CanSave must reject it before Build runs.");
	}
}
