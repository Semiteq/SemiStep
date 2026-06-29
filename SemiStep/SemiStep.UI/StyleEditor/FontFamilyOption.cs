namespace SemiStep.UI.StyleEditor;

/// <summary>
/// A font-family choice for the editor's family picker: a display <see cref="Name"/> bound to its
/// stored <see cref="Value"/>. The empty-string value (theme default) shows as "(Default)". The
/// ComboBox shows <see cref="Name"/> and round-trips <see cref="Value"/> via its selected-value binding.
/// </summary>
public sealed record FontFamilyOption(string Value, string Name);
