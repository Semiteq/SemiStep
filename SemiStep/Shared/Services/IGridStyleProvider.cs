using Shared.Entities;

namespace Shared.Services;

public interface IGridStyleProvider
{
	GridStyleOptions Current { get; }

	event EventHandler<GridStyleOptions>? StylesChanged;

	void Initialize(GridStyleOptions options);

	void UpdateStyles(GridStyleOptions newOptions);
}
