namespace Core.Exceptions;

public sealed class InvalidStepIndexException : CoreException
{
	public int Index { get; }
	public int MaxIndex { get; }

	public InvalidStepIndexException(int index, int maxIndex)
		: base($"Step index {index} is out of range [0, {maxIndex}]")
	{
		Index = index;
		MaxIndex = maxIndex;
	}
}
