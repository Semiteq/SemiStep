namespace Csv;

public sealed class CsvHeaderMismatchException : Exception
{
	public CsvHeaderMismatchException(string[] expected, string[] actual)
		: base(FormatMessage(expected, actual))
	{
		Expected = expected;
		Actual = actual;
	}

	public string[] Expected { get; }
	public string[] Actual { get; }

	private static string FormatMessage(string[] expected, string[] actual)
	{
		return $"CSV header mismatch. Expected: [{string.Join("; ", expected)}], " +
			   $"Actual: [{string.Join("; ", actual)}]";
	}
}
