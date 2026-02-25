namespace Csv;

public sealed class CsvParseException : Exception
{
	public CsvParseException(string details, int? lineNumber = null)
		: base(FormatMessage(details, lineNumber))
	{
		Details = details;
		LineNumber = lineNumber;
	}

	public string Details { get; }
	public int? LineNumber { get; }

	private static string FormatMessage(string details, int? lineNumber)
	{
		return lineNumber.HasValue
			? $"CSV parse error at line {lineNumber}: {details}"
			: $"CSV parse error: {details}";
	}
}
