// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 消息目录的注册扩展
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// 注册消息目录
    /// </summary>
    /// <remarks>
    /// 同名目录合并为一份，留在先注册的位置：内容相同（多个 Provider 都带同一个抽象包的目录）时什么都不变；互为补充（如插件带两种语言、宿主包带全部十种）时区域与键取并集。同一键同一区域的模板不同抛出携带 <see cref="LocalizationErrors.DuplicateCatalog"/> 的 <see cref="FrameworkException"/>，不丢弃任何一份
    /// </remarks>
    public static IServiceCollection AddMessageCatalog(this IServiceCollection services, MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        for (var index = 0; index < services.Count; index++)
        {
            if (services[index].ServiceType != typeof(MessageCatalog) || services[index].ImplementationInstance is not MessageCatalog existing || !string.Equals(existing.Name, catalog.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var merged = existing.MergeWith(catalog);
            if (!ReferenceEquals(merged, existing))
            {
                services[index] = ServiceDescriptor.Singleton(merged);
            }

            return services;
        }

        services.AddSingleton(catalog);
        return services;
    }
}
