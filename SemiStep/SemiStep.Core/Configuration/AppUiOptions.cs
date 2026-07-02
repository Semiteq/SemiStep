namespace SemiStep.Core.Configuration;

public sealed record AppUiOptions(string Locale)
{
	public const string DefaultLocale = "ru";

	public static AppUiOptions Default { get; } = new(DefaultLocale);
}
