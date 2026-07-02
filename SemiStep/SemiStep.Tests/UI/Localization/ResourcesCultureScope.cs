using System;
using System.Globalization;

using SemiStep.UI.Localization;

namespace SemiStep.Tests.UI.Localization;

internal sealed class ResourcesCultureScope : IDisposable
{
	private readonly CultureInfo? _previousCulture;

	private ResourcesCultureScope(string culture)
	{
		_previousCulture = Resources.Culture;
		Resources.Culture = new CultureInfo(culture);
	}

	public static IDisposable Use(string culture)
	{
		return new ResourcesCultureScope(culture);
	}

	public void Dispose()
	{
		Resources.Culture = _previousCulture;
	}
}
