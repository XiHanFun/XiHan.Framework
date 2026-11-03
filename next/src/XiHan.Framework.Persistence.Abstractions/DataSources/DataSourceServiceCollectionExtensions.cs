// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 运行时数据源的登记
/// </summary>
/// <remarks>
/// 持久化 Provider 在装配时调用：支持运行时数据源的登记自己的 <see cref="IDataSourceProvider"/>（<see cref="AddDataSourceProvider"/>），不支持的只登记注册表（<see cref="AddDataSourceRegistry"/>），取用时得到明确的 <see cref="PersistenceErrors.NotSupported"/>。注册表与它的健康项只登记一次
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class DataSourceServiceCollectionExtensions
{
    /// <summary>
    /// 登记一个 Provider（同一类型只登记一次），并确保注册表已登记
    /// </summary>
    /// <remarks>
    /// 选项从 <paramref name="configuration"/> 的 <c>XiHan:Persistence:DataSources</c> 读取，值不合法时在这里抛出 <see cref="ArgumentException"/>（装配即失败）；先登记的那份选项为准
    /// </remarks>
    public static IServiceCollection AddDataSourceProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services, IConfiguration configuration)
        where TProvider : class, IDataSourceProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(DataSourceOptions.FromConfiguration(configuration));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDataSourceProvider, TProvider>());
        return services.AddDataSourceRegistry();
    }

    /// <summary>
    /// 登记注册表 <see cref="DataSourceRegistry"/>（单例）与它的健康项（只登记一次）
    /// </summary>
    /// <remarks>
    /// 不支持运行时数据源的 Provider 只调这个：没有 <see cref="IDataSourceProvider"/> 的注册表取用与测试连接都返回 <see cref="PersistenceErrors.NotSupported"/>。容器里没有 <see cref="DataSourceOptions"/> 时用缺省选项
    /// </remarks>
    public static IServiceCollection AddDataSourceRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(static d => d.ServiceType == typeof(DataSourceRegistry)))
        {
            services.AddSingleton(static sp => new DataSourceRegistry(
                sp.GetServices<IDataSourceProvider>(),
                sp.GetService<DataSourceOptions>() ?? new DataSourceOptions(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetService<ILoggerFactory>()));
            services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<DataSourceRegistry>());
        }

        return services;
    }
}
