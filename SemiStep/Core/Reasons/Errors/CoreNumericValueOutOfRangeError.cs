namespace Core.Reasons.Errors;

public sealed class CoreNumericValueOutOfRangeError : BilingualError
{
	public float Value { get; }
	public float? Min { get; }
	public float? Max { get; }

	public CoreNumericValueOutOfRangeError(float value, float? min, float? max)
		: base(
			$"Value {value} is out of allowed range [{min ?? float.MinValue}, {max ?? float.MaxValue}]",
			$"Значение {value} находится вне допустимого диапазона [{min ?? float.MinValue}, {max ?? float.MaxValue}]")
	{
		Value = value;
		Min = min;
		Max = max;
	}
}
