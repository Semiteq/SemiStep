namespace Core.Reasons.Errors;

public sealed class CoreColumnGroupNameEmptyError : BilingualError
{
	public CoreColumnGroupNameEmptyError()
		: base(
			"Column GroupName is empty",
			"GroupName столбца пуст")
	{
	}
}
