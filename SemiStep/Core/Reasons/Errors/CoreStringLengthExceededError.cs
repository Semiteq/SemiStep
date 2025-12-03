namespace Core.Reasons.Errors;

public sealed class CoreStringLengthExceededError : BilingualError
{
	public int CurrentLength { get; }
	public int MaxLength { get; }

	public CoreStringLengthExceededError(int currentLength, int maxLength)
		: base(
			$"String length {currentLength} exceeds maximum allowed length of {maxLength}",
			$"Длина строки {currentLength} превышает максимально допустимую длину {maxLength}")
	{
		CurrentLength = currentLength;
		MaxLength = maxLength;
	}
}
