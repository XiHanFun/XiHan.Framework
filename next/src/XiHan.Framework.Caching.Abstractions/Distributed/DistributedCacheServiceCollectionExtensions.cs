// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Distributed;

/// <summary>
/// 把 <see cref="IDistributedCache"/> 登记为架在 <see cref="ICacheStore"/> 上的适配器。提供 <c>caching.distributed@1</c> 的 Provider 在被选中时调用；已有登记（应用自己的 <see cref="IDistributedCache"/>）优先。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class DistributedCacheServiceCollectionExtensions
{
    /// <summary>
    /// 以 <paramref name="store"/> 解析出的存储登记 <see cref="IDistributedCache"/>（幂等：已有登记时不覆盖）。时钟取容器里的 <see cref="TimeProvider"/>，没有则用系统时钟。
    /// </summary>
    public static IServiceCollection AddXiHanDistributedCache(this IServiceCollection services, Func<IServiceProvider, ICacheStore> store)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);
        services.TryAddSingleton<IDistributedCache>(sp => new CacheStoreDistributedCache(store(sp), sp.GetService<TimeProvider>() ?? TimeProvider.System));
        return services;
    }
}
