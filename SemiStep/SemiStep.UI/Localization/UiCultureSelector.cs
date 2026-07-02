using System.Globalization;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Localization;

public static class UiCultureSelector
{
	// ICU synthesises an unnamed custom culture (this LCID) instead of throwing for
	// structurally-valid but unknown tags. Treat those as invalid and fall back.
	private const int UnknownCustomCultureLcid = 0x1000;

	public static CultureInfo Resolve(string? locale)
	{
		if (string.IsNullOrWhiteSpace(locale))
		{
			return Fallback();
		}

		try
		{
			var culture = CultureInfo.GetCultureInfo(locale);

			if (culture.LCID == UnknownCustomCultureLcid)
			{
				return Fallback();
			}

			return culture;
		}
		catch (CultureNotFoundException)
		{
			return Fallback();
		}
	}

	private static CultureInfo Fallback()
	{
		return new CultureInfo(AppUiOptions.DefaultLocale);
	}
}
