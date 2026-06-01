using System.Text;

using SemiStep.Core.Plc.Configuration;

namespace SemiStep.Core.Plc.Sync.Ownership;

public static class SyncOwnershipEndpointToken
{
	public static string For(PlcConnectionSettings endpoint)
	{
		var raw = $"{endpoint.IpAddress}_{endpoint.Port}_{endpoint.Rack}_{endpoint.Slot}";
		return Sanitize(raw);
	}

	private static string Sanitize(string value)
	{
		var invalidCharacters = Path.GetInvalidFileNameChars();
		var builder = new StringBuilder(value.Length);

		foreach (var character in value)
		{
			builder.Append(IsAllowed(character, invalidCharacters) ? character : '_');
		}

		return builder.ToString();
	}

	private static bool IsAllowed(char character, char[] invalidCharacters)
	{
		if (character == '.' || character == '_')
		{
			return true;
		}

		if (!char.IsLetterOrDigit(character))
		{
			return false;
		}

		return Array.IndexOf(invalidCharacters, character) < 0;
	}
}
