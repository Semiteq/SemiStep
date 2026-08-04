using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleConfigMissingError()
	: Error("Grid style configuration is missing (ui/grid_style.yaml).")
{
}
