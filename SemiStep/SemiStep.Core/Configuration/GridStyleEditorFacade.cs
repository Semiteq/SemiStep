using FluentResults;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;

namespace SemiStep.Core.Configuration;

/// <summary>
/// The single public Core seam for the in-app style editor: <see cref="Load"/> and <see cref="Save"/>.
/// Color hex validation now lives in <see cref="GridStyleMapper"/>, which parses and validates in one pass
/// on <see cref="Load"/>; an invalid color is unrepresentable in <see cref="GridStyleOptions"/>, so
/// <see cref="Save"/> writes without a pre-write gate. Numeric-range validation (font sizes, paddings, row
/// height, spacing, panel height) is the caller's responsibility — the editor view model enforces those
/// bounds before invoking <see cref="Save"/>.
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

	public async Task<Result> Save(string configDir, GridStyleOptions options)
	{
		return await _gridStyleWriter.SaveAsync(configDir, options);
	}
}
