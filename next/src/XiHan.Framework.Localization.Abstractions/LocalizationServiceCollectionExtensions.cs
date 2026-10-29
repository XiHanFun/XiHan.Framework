// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 消息目录的注册扩展。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// 注册消息目录。
    /// </summary>
    /// <remarks>
    /// 同名目录只保留第一次注册（多个 Provider 可能都带同一份抽象包目录）。
    /// </remarks>
    public static IServiceCollection AddMessageCatalog(this IServiceCollection services, MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var descriptor in services)
        {
            // 同名目录视为同一份，跳过重复注册
            if (descriptor.ServiceType == typeof(MessageCatalog) && descriptor.ImplementationInstance is MessageCatalog existing && string.Equals(existing.Name, catalog.Name, StringComparison.Ordinal))
            {
                return services;
            }
        }

        services.AddSingleton(catalog);
        return services;
    }
}
