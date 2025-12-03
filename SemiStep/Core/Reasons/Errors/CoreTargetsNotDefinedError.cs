namespace Core.Reasons.Errors;

public sealed class CoreTargetsNotDefinedError : BilingualError
{
	public CoreTargetsNotDefinedError()
		: base(
			"No targets defined for group",
			"Не определены цели для группы")
	{
	}
}
