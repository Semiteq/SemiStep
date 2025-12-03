namespace Core.Reasons.Errors;

public sealed class CoreIndexOutOfRangeError : BilingualError
{
	public int Index { get; }
	public int Count { get; }

	public CoreIndexOutOfRangeError(int index, int count)
		: base(
			$"Index {index} is out of range (total: {count})",
			$"Индекс {index + 1} вне диапазона (всего: {count})")
	{
		Index = index;
		Count = count;
	}
}
