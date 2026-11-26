using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace SemiStep.UI.RecipeGrid;

/// <summary>The "(pass/count)" pill of a running loop in the action cell of its "Цикл начало" row.</summary>
internal static class LoopBadge
{
	public const string PillClass = "loop-badge";

	public static Border Create(string loopPassTextPath)
	{
		var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
		text.Bind(TextBlock.TextProperty, new Binding(loopPassTextPath) { Mode = BindingMode.OneWay });

		var pill = new Border
		{
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
			Child = text,
		};
		pill.Classes.Add(PillClass);
		pill.Bind(Visual.IsVisibleProperty, new Binding(loopPassTextPath)
		{
			Mode = BindingMode.OneWay,
			Converter = StringConverters.IsNotNullOrEmpty,
		});

		return pill;
	}
}
