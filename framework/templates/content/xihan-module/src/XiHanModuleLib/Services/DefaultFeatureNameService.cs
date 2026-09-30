using Microsoft.Extensions.Options;
using XiHanModuleLib.Options;

namespace XiHanModuleLib.Services;

/// <summary>
/// FeatureName 服务默认实现
/// </summary>
/// <param name="options">FeatureName 选项</param>
public class DefaultFeatureNameService(IOptions<FeatureNameOptions> options) : IFeatureNameService
{
    /// <inheritdoc />
    public Task<string> GreetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult($"{options.Value.GreetingPrefix}，{name.Trim()}！");
    }
}
