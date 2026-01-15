using Core.Init;

using Microsoft.Extensions.DependencyInjection;

namespace Core;

public class Core
{
	public static void Main()
	{
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddCore();
	}
}
