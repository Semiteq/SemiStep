using System.Globalization;

namespace SemiStep.Tests.Helpers;

public sealed class CultureScope : IDisposable
{
	private readonly CultureInfo _originalCulture;
	private readonly CultureInfo _originalUiCulture;

	public CultureScope(string cultureName)
	{
		_originalCulture = Thread.CurrentThread.CurrentCulture;
		_originalUiCulture = Thread.CurrentThread.CurrentUICulture;

		var culture = new CultureInfo(cultureName);
		Thread.CurrentThread.CurrentCulture = culture;
		Thread.CurrentThread.CurrentUICulture = culture;
	}

	public void Dispose()
	{
		Thread.CurrentThread.CurrentCulture = _originalCulture;
		Thread.CurrentThread.CurrentUICulture = _originalUiCulture;
	}
}
