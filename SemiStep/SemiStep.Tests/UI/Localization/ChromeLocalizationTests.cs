using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using FluentAssertions;

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
	public void RecipeMenuBar_UnderRussianCulture_RendersRussianHeaders()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			var menuBar = new RecipeMenuBar();
			var headers = menuBar.GetLogicalDescendants()
				.OfType<MenuItem>()
				.Select(item => item.Header as string)
				.ToList();

			headers.Should().Contain("_Файл");
			headers.Should().Contain("_Правка");
		}
	}

	[Fact]
	public void DialogKeys_UnderRussianCulture_ResolveToRussianValues()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			Resources.DialogSave.Should().Be("Сохранить");
			Resources.DialogCancel.Should().Be("Отмена");
			Resources.PlcConflictKeepLocal.Should().Be("Оставить локальный");
		}
	}

	[Fact]
	public void ChromeKeys_UnderEnglishCulture_ResolveToNeutralValues()
	{
		using (ResourcesCultureScope.Use("en"))
		{
			Resources.MenuFile.Should().Be("_File");
			Resources.DialogSave.Should().Be("Save");
			Resources.PlcConflictKeepLocal.Should().Be("Keep local");
		}
	}
}
