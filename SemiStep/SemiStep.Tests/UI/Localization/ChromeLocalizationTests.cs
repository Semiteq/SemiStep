using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using AwesomeAssertions;

using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class ChromeLocalizationTests
{
	[AvaloniaFact]
	public void RecipeMenuBar_UnderRussianCulture_RendersHeadersFromResources()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			var menuBar = new RecipeMenuBar();
			var headers = menuBar.GetLogicalDescendants()
				.OfType<MenuItem>()
				.Select(item => item.Header as string)
				.ToList();

			headers.Should().Contain(Resources.MenuFile);
			headers.Should().Contain(Resources.MenuEdit);
		}
	}
}
