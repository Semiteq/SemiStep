using Avalonia.Controls;

using Shared.Entities;

using UI.Helpers;

namespace UI.Controls;

public partial class ValidationErrorsPanel : UserControl
{
	public ValidationErrorsPanel()
	{
		InitializeComponent();
	}

	public void ApplyStyles(GridStyleOptions styles)
	{
		var errorsList = this.FindControl<ListBox>("ErrorsList");
		if (errorsList is null)
		{
			return;
		}

		errorsList.Background = StyleHelper.ToBrush(styles.ValidationPanelBackgroundColor);
		errorsList.Foreground = StyleHelper.ToBrush(styles.ValidationPanelForegroundColor);
		errorsList.MaxHeight = styles.ValidationPanelMaxHeight;
	}
}
