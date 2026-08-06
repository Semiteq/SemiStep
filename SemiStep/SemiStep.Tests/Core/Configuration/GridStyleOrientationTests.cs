using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleOrientation")]
public sealed class GridStyleOrientationTests
{
	[Theory]
	[InlineData("rows_as_steps", GridOrientation.RowsAsSteps)]
	[InlineData("columns_as_steps", GridOrientation.ColumnsAsSteps)]
	public async Task Load_ExplicitOrientation_SurfacesOnRecord(string yamlValue, GridOrientation expected)
	{
		using var tempDir = CopyShippedConfig("MBE");
		await AppendOrientation(tempDir.Path, yamlValue);

		var options = await LoadValidated(tempDir.Path);

		options.Orientation.Should().Be(expected);
	}

	[Fact]
	public async Task Load_MissingOrientation_DefaultsToRowsAsSteps()
	{
		using var tempDir = CopyShippedConfig("MBE");

		var options = await LoadValidated(tempDir.Path);

		options.Orientation.Should().Be(GridOrientation.RowsAsSteps);
	}

	[Theory]
	[InlineData("diagonal")]
	[InlineData("ROWS_AS_STEPS")]
	[InlineData("Columns_As_Steps")]
	public async Task Validate_UnknownOrientation_Fails(string yamlValue)
	{
		using var tempDir = CopyShippedConfig("MBE");
		await AppendOrientation(tempDir.Path, yamlValue);

		var loadResult = await GridStyleLoader.LoadAsync(tempDir.Path);
		loadResult.IsSuccess.Should().BeTrue();

		var validation = GridStyleMapper.Map(loadResult.Value);

		validation.IsFailed.Should().BeTrue();
		validation.Errors.Should().Contain(error =>
			error.Message.Contains("orientation") && error.Message.Contains(yamlValue));
	}

	[Fact]
	public void Parse_UnknownValue_Throws()
	{
		var act = () => GridOrientationValues.Parse("diagonal");

		act.Should().Throw<ArgumentException>().WithMessage("*diagonal*");
	}

	[Fact]
	public void Parse_NullOrKnownValues_MapWithoutValidator()
	{
		GridOrientationValues.Parse(null).Should().Be(GridOrientation.RowsAsSteps);
		GridOrientationValues.Parse("rows_as_steps").Should().Be(GridOrientation.RowsAsSteps);
		GridOrientationValues.Parse("columns_as_steps").Should().Be(GridOrientation.ColumnsAsSteps);
	}

	[Fact]
	public async Task SaveRoundTrip_PreservesOrientation()
	{
		using var tempDir = CopyShippedConfig("MBE");
		await AppendOrientation(tempDir.Path, "columns_as_steps");
		var original = await LoadValidated(tempDir.Path);

		(await new GridStyleWriter().SaveAsync(tempDir.Path, original)).IsSuccess.Should().BeTrue();

		var reloaded = await LoadValidated(tempDir.Path);
		reloaded.Orientation.Should().Be(GridOrientation.ColumnsAsSteps);
		reloaded.Should().Be(original);
	}

	[Fact]
	public async Task Save_MissingOrientation_EmitsExplicitDefault()
	{
		using var tempDir = CopyShippedConfig("MOCVD");
		var options = await LoadValidated(tempDir.Path);

		(await new GridStyleWriter().SaveAsync(tempDir.Path, options)).IsSuccess.Should().BeTrue();

		var content = await File.ReadAllTextAsync(
			Path.Combine(tempDir.Path, "ui", "grid_style.yaml"),
			TestContext.Current.CancellationToken);
		content.Should().Contain("orientation: rows_as_steps");
	}

	[Theory]
	[InlineData("RIE", GridOrientation.ColumnsAsSteps)]
	[InlineData("MBE", GridOrientation.RowsAsSteps)]
	[InlineData("MOCVD", GridOrientation.RowsAsSteps)]
	public async Task ShippedConfig_CarriesExpectedOrientation(string equipment, GridOrientation expected)
	{
		var options = await LoadValidated(ShippedConfigLocator.GetConfigDirectory(equipment));

		options.Orientation.Should().Be(expected);
	}

	private static async Task<GridStyleOptions> LoadValidated(string configDir)
	{
		var loadResult = await GridStyleLoader.LoadAsync(configDir);
		loadResult.IsSuccess.Should().BeTrue();
		var mapResult = GridStyleMapper.Map(loadResult.Value);
		mapResult.IsSuccess.Should().BeTrue();
		return mapResult.Value;
	}

	private static TempDirectory CopyShippedConfig(string equipment)
	{
		var source = ShippedConfigLocator.GetConfigDirectory(equipment);
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		File.Copy(
			Path.Combine(source, "ui", "grid_style.yaml"),
			Path.Combine(uiDir, "grid_style.yaml"),
			overwrite: true);
		return tempDir;
	}

	private static Task AppendOrientation(string configDir, string value)
	{
		var filePath = Path.Combine(configDir, "ui", "grid_style.yaml");
		return File.AppendAllTextAsync(
			filePath,
			$"\norientation: {value}\n",
			TestContext.Current.CancellationToken);
	}
}
