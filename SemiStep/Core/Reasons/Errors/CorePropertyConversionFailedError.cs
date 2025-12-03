namespace Core.Reasons.Errors;

public sealed class CorePropertyConversionFailedError : BilingualError
{
	public string Input { get; }
	public string TargetType { get; }

	public CorePropertyConversionFailedError(string input, string targetType)
		: base(
			$"Unable to parse '{input}' as {targetType}",
			$"Не удалось преобразовать '{input}' в {targetType}")
	{
		Input = input;
		TargetType = targetType;
	}
}
