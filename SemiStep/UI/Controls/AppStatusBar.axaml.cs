using Avalonia.Controls;

using Shared.Entities;

using UI.Helpers;

namespace UI.Controls;

public partial class AppStatusBar : UserControl
{
	public AppStatusBar()
	{
		InitializeComponent();
	}

	public void ApplyStyles(GridStyleOptions styles)
	{
		var border = this.FindControl<Border>("StatusBarBorder");
		var panel = this.FindControl<StackPanel>("StatusBarPanel");

		if (border is not null)
		{
			border.Background = StyleHelper.ToBrush(styles.StatusBarBackgroundColor);
			border.Padding = StyleHelper.ToUniformThickness(styles.StatusBarPadding);
		}

		if (panel is not null)
		{
			panel.Spacing = styles.StatusBarItemSpacing;
		}
	}
}
