using Config.Models;

namespace Config.Loaders;

public interface ISectionLoader<TResult>
{
	Task<TResult> LoadAsync(string configDirectory, ConfigContext context);
}
