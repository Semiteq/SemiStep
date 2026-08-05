using FluentResults;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Core.Configuration.Validation;

namespace SemiStep.Core.Configuration;

/// <summary>
/// The single public Core seam for the in-app style editor. <see cref="Validate"/> and
/// <see cref="Save"/> check color hex format only; numeric-range validation (font sizes, paddings,
/// row height, spacing, panel height) is the caller's responsibility — the editor view model enforces
/// those bounds before invoking <see cref="Save"/>.
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

		var validation = GridStyleValidator.Validate(loadResult.Value);
		if (validation.IsFailed)
		{
			return validation.ToResult<GridStyleOptions>();
		}

		return Result.Ok(GridStyleMapper.Map(loadResult.Value));
	}

	public Result Validate(GridStyleOptions options)
	{
		return GridStyleValidator.Validate(GridStyleDtoMapper.Map(options));
	}

	public async Task<Result> Save(string configDir, GridStyleOptions options)
	{
		var validation = Validate(options);
		if (validation.IsFailed)
		{
			return validation;
		}

		return await _gridStyleWriter.SaveAsync(configDir, options);
	}
}
