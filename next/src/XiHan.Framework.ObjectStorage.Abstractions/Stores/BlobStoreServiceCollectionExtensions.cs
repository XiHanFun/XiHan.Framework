// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 命名存储实例的登记：存储包在装配时登记自己的 <see cref="IBlobStoreProvider"/>，注册表 <see cref="BlobStores"/> 与它的健康项只登记一次。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobStoreServiceCollectionExtensions
{
    /// <summary>
    /// 登记一个 Provider（同一类型只登记一次），并确保 <see cref="BlobStores"/> 已登记为单例与健康项贡献者；配置定义的实例从 <paramref name="configuration"/> 读（先登记的那次为准，特性都传宿主配置）。
    /// </summary>
    public static IServiceCollection AddBlobStoreProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services, IConfiguration configuration)
        where TProvider : class, IBlobStoreProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBlobStoreProvider, TProvider>());
        if (!services.Any(static d => d.ServiceType == typeof(BlobStores)))
        {
            services.AddSingleton(sp => new BlobStores(sp.GetServices<IBlobStoreProvider>(), sp.GetRequiredService<TimeProvider>(), configuration));
            services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<BlobStores>());
        }

        return services;
    }
}
