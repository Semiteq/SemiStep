using System.Collections.Immutable;

namespace Csv;

internal sealed record CsvMetadata(
	char Separator = ';',
	int Rows = 0,
	ImmutableDictionary<string, string>? Extras = null)
{
	public ImmutableDictionary<string, string> Extras { get; init; } =
		Extras ?? ImmutableDictionary<string, string>.Empty;

	public static void Serialize(TextWriter writer, CsvMetadata metadata)
	{
		writer.WriteLine($"# SEP=\"{metadata.Separator}\"");
		writer.WriteLine($"# ROWS=\"{metadata.Rows}\"");

		foreach (var kv in metadata.Extras)
		{
			writer.WriteLine($"# X_{kv.Key}=\"{kv.Value}\"");
		}
	}

	public static (CsvMetadata Metadata, int LinesConsumed) Deserialize(string fullText)
	{
		using var reader = new StringReader(fullText);
		var extras = ImmutableDictionary.CreateBuilder<string, string>();
		var separator = ';';
		var rows = 0;
		var linesConsumed = 0;

		string? line;
		while ((line = reader.ReadLine()) is not null)
		{
			if (!line.StartsWith('#'))
			{
				break;
			}

			linesConsumed++;

			var payload = line.TrimStart('#', ' ');
			var equalsIndex = payload.IndexOf('=');
			if (equalsIndex <= 0)
			{
				continue;
			}

			var key = payload[..equalsIndex].Trim();
			var rawValue = payload[(equalsIndex + 1)..];
			var value = Unquote(rawValue);

			switch (key.ToUpperInvariant())
			{
				case "SEP":
					separator = value.Length > 0 ? value[0] : ';';

					break;

				case "ROWS":
					if (int.TryParse(value, out var parsedRows))
					{
						rows = parsedRows;
					}

					break;

				default:
					if (key.StartsWith("X_", StringComparison.OrdinalIgnoreCase))
					{
						extras[key[2..]] = value;
					}

					break;
			}
		}

		var metadata = new CsvMetadata(separator, rows, extras.ToImmutable());

		return (metadata, linesConsumed);
	}

	private static string Unquote(string value)
	{
		var trimmed = value.Trim();

		if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
		{
			return trimmed[1..^1];
		}

		return trimmed;
	}
}
