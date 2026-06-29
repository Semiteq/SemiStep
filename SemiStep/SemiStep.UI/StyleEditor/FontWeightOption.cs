namespace SemiStep.UI.StyleEditor;

/// <summary>
/// A weight choice for the editor's weight pickers: a display name bound to its numeric
/// <see cref="Value"/> (100-900). The ComboBox shows <see cref="Name"/> and round-trips
/// <see cref="Value"/> via its selected-value binding.
/// </summary>
public sealed record FontWeightOption(int Value, string Name);
