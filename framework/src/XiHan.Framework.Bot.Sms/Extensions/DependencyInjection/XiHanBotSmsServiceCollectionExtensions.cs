#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotSmsServiceCollectionExtensions
// Guid:0ab391a6-37f6-4c18-9a76-13e22d35d4cf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Sms.Extensions.DependencyInjection;

/// <summary>
/// 短信 Bot 服务注册扩展
/// </summary>
public static class XiHanBotSmsServiceCollectionExtensions
{
    /// <summary>
    /// 注册短信 Bot 提供者、网关解析器与配置存储
    /// </summary>
    /// <remarks>
    /// 默认配置存储恒返回 null（短信凭证不宜放配置文件）；
    /// 应用层须以数据库实现覆盖 <see cref="ISmsConfigStore"/>（本方法为 TryAdd 语义）。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotSms(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISmsConfigStore, DefaultSmsConfigStore>();
        services.TryAddSingleton<ISmsGatewayResolver, SmsGatewayResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, SmsBotProvider>());

        return services;
    }
}
