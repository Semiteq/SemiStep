using System.Collections.Immutable;
using System.Globalization;

using Domain.Ports;

using Recipe.Entities;

namespace Csv;

public sealed class CsvRecipeRepository : IRecipeRepository
{
	private const char Separator = ';';
	private const string ActionKeyColumn = "ActionKey";

	public bool CanHandle(string filePath)
	{
		return filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
	}

	public async Task<Recipe.Entities.Recipe> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"Recipe file not found: {filePath}");
		}

		var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

		if (lines.Length < 2)
		{
			return Recipe.Entities.Recipe.Empty;
		}

		var headers = lines[0].Split(Separator);
		var steps = new List<Step>();

		for (int i = 1; i < lines.Length; i++)
		{
			if (string.IsNullOrWhiteSpace(lines[i]))
			{
				continue;
			}

			var step = ParseStep(lines[i], headers, i);
			steps.Add(step);
		}

		return new Recipe.Entities.Recipe(steps.ToImmutableList());
	}

	public async Task SaveAsync(Recipe.Entities.Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		var lines = new List<string>();

		var allColumns = recipe.Steps
			.SelectMany(s => s.Properties.Keys)
			.Distinct()
			.OrderBy(c => c.Value)
			.ToList();

		var headerColumns = new List<string> { ActionKeyColumn };
		headerColumns.AddRange(allColumns.Select(c => c.Value));
		lines.Add(string.Join(Separator, headerColumns));

		foreach (var step in recipe.Steps)
		{
			var rowValues = new List<string> { step.ActionKey };
			rowValues.AddRange(allColumns.Select(col =>
				step.Properties.TryGetValue(col, out var value)
					? FormatValue(value)
					: string.Empty));
			lines.Add(string.Join(Separator, rowValues));
		}

		await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
	}

	private static Step ParseStep(string line, string[] headers, int lineNumber)
	{
		var values = line.Split(Separator);
		var properties = ImmutableDictionary.CreateBuilder<ColumnId, PropertyValue>();
		string actionKey = string.Empty;

		for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
		{
			var header = headers[j].Trim();
			var rawValue = values[j].Trim();

			if (string.IsNullOrEmpty(rawValue))
			{
				continue;
			}

			if (header.Equals(ActionKeyColumn, StringComparison.OrdinalIgnoreCase))
			{
				actionKey = rawValue;
				continue;
			}

			var columnId = new ColumnId(header);
			var propertyValue = ParseValue(rawValue);
			properties[columnId] = propertyValue;
		}

		return new Step(actionKey, properties.ToImmutable());
	}

	private static PropertyValue ParseValue(string rawValue)
	{
		if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
		{
			return new PropertyValue(intValue, PropertyType.Int);
		}

		if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
		{
			return new PropertyValue(floatValue, PropertyType.Float);
		}

		return new PropertyValue(rawValue, PropertyType.String);
	}

	private static string FormatValue(PropertyValue value)
	{
		return value.Type switch
		{
			PropertyType.Int => ((int)value.Value).ToString(CultureInfo.InvariantCulture),
			PropertyType.Float => ((float)value.Value).ToString("G", CultureInfo.InvariantCulture),
			PropertyType.String => (string)value.Value,
			_ => value.Value?.ToString() ?? string.Empty
		};
	}
}
