using FluentResults;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;

namespace SemiStep.Core.Configuration;

/// <summary>
/// The single public Core seam for the in-app style editor. Color hex validation now lives in
/// <see cref="GridStyleMapper"/>, which parses and validates in one pass on <see cref="Load"/>; an invalid
/// color is unrepresentable in <see cref="GridStyleOptions"/>, so <see cref="Save"/> writes without a
/// pre-write gate. Numeric-range validation (font sizes, paddings, row height, spacing, panel height) is the
/// caller's responsibility — the editor view model enforces those bounds before invoking <see cref="Save"/>.
/// </summary>
public sealed class GridStyleEditorFacade : IGridStyleEditorFacade
{
	private readonly GridStyleWriter _gridStyleWriter = new();

	public async Task<Result<GridStyleOptions>> Load(string configDir)
	{
		var loadResult = await GridStyleLoader.LoadAsync(configDir);
		if (loadResult.IsFailed)
		{
			return loadResult.ToResult<GridStyleOptions>();
		}

		return GridStyleMapper.Map(loadResult.Value);
	}

	public Result Validate(GridStyleOptions options)
	{
		// Deliberate vacuous pass-through: a typed GridStyleOptions cannot hold an invalid color, so there is
		// nothing to validate here. The method stays on IGridStyleEditorFacade until slice 5 trims the interface.
		return Result.Ok();
	}

	public async Task<Result> Save(string configDir, GridStyleOptions options)
	{
		return await _gridStyleWriter.SaveAsync(configDir, options);
	}
}
