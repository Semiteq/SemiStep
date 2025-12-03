namespace Core.Reasons.Errors;

public sealed class CoreTimeComponentOutOfRangeError : BilingualError
{
	public string Component { get; }
	public int Value { get; }
	public int MaxValue { get; }

	public CoreTimeComponentOutOfRangeError(string component, int value, int maxValue)
		: base(
			$"Invalid {component} value: {value} (must be 0-{maxValue})",
			$"Недопустимое значение {component}: {value} (должно быть 0-{maxValue})")
	{
		Component = component;
		Value = value;
		MaxValue = maxValue;
	}
}
