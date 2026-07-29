using System.Globalization;

namespace SemiStep.UI.Localization;

// Carries a resx key so a command-error context can render both an invariant/English form for
// the log and a current-culture form for the panel. No implicit conversion from string exists,
// so a raw literal can never masquerade as a key.
public readonly record struct LocalizedText(string ResourceKey)
{
	public string Localized => Resources.ResourceManager.GetString(ResourceKey, Resources.Culture) ?? ResourceKey;

	public string Invariant => Resources.ResourceManager.GetString(ResourceKey, CultureInfo.InvariantCulture) ?? ResourceKey;
}
