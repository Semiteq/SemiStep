using FluentResults;

namespace SemiStep.Core.Configuration;

/// <summary>
/// The style-editor seam consumed by the UI editor view model. Declared as an interface so the
/// view model depends on an abstraction it can mock, rather than the concrete Core facade.
/// </summary>
public interface IGridStyleEditorFacade
{
	Task<Result<GridStyleOptions>> Load(string configDir);

	Result Validate(GridStyleOptions options);

	Result Save(string configDir, GridStyleOptions options);
}
