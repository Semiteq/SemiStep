using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class PropertiesSectionLoader
{
	public static Task<Result<List<PropertyDto>>> LoadAsync(string configDirectory)
	{
		return MergeSectionLoader.LoadAsync<string, PropertyDto, PropertyDto>(
			configDirectory,
			ConfigFolderPolicy.PropertiesFolder,
			StringComparer.OrdinalIgnoreCase,
			MapProperty);
	}

	private static Result<PropertyDto> MapProperty(string propertyTypeId, PropertyDto propertyContent, string fileName)
	{
		var property = new PropertyDto
		{
			PropertyTypeId = propertyTypeId,
			SystemType = propertyContent.SystemType,
			FormatKind = propertyContent.FormatKind,
			Units = propertyContent.Units,
			Min = propertyContent.Min,
			Max = propertyContent.Max,
			MaxLength = propertyContent.MaxLength
		};

		return ValidateProperty(property, fileName).ToResult(property);
	}

	private static Result ValidateProperty(
		PropertyDto property,
		string fileName)
	{
		var location = $"{fileName}, PropertyTypeId='{property.PropertyTypeId}'";

		if (string.IsNullOrWhiteSpace(property.PropertyTypeId))
		{
			return Result.Fail($"[{fileName}] Property PropertyTypeId is required");
		}

		var validationResults = new List<Result>();

		if (string.IsNullOrWhiteSpace(property.SystemType))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Property SystemType is required"));
		}
		else
		{
			var validSystemTypes = new[] { "int", "float", "string" };
			if (!validSystemTypes.Contains(property.SystemType, StringComparer.OrdinalIgnoreCase))
			{
				validationResults.Add(Result.Fail(
					$"[{location}] Property SystemType must be one of: {string.Join(", ", validSystemTypes)}, got: '{property.SystemType}'"));
			}
		}

		if (string.IsNullOrWhiteSpace(property.FormatKind))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Property FormatKind is required"));
		}

		if (property.Min.HasValue && property.Max.HasValue && property.Min > property.Max)
		{
			validationResults.Add(Result.Fail(
				$"[{location}] Property Min ({property.Min}) cannot be greater than Max ({property.Max})"));
		}

		return validationResults.Count == 0 ? Result.Ok() : Result.Merge([.. validationResults]);
	}
}
