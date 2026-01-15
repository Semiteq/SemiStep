using FluentResults;

namespace Core.Reasons;

internal class Warning : Success
{
	internal Warning(string message) : base(message)
	{

	}
}
