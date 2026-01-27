using Shared.Entities;
using Shared.Services;

namespace Domain.Services;

public sealed class GridStyleProvider : IGridStyleProvider
{
	private GridStyleOptions _current = GridStyleOptions.Default;

	public GridStyleOptions Current => _current;

	public event EventHandler<GridStyleOptions>? StylesChanged;

	public void Initialize(GridStyleOptions options)
	{
		_current = options;
	}

	public void UpdateStyles(GridStyleOptions newOptions)
	{
		_current = newOptions;
		StylesChanged?.Invoke(this, newOptions);
	}
}
