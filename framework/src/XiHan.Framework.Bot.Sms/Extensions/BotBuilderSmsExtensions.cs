// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Stores;

namespace XiHan.Framework.Bot.Sms.Extensions;

/// <summary>
/// BotBuilder 短信扩展
/// </summary>
public static class BotBuilderSmsExtensions
{
    /// <summary>
    /// 启用短信提供者
    /// </summary>
    /// <remarks>
    /// 短信凭证不走选项绑定（不宜放配置文件），仅注册提供者与解析器；
    /// 配置由 <see cref="ISmsConfigStore"/> 提供，应用层须注册数据库实现覆盖默认空实现。
    /// </remarks>
    /// <param name="builder">Bot 构建器</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseSms(this BotBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<ISmsConfigStore, DefaultSmsConfigStore>();
        builder.Services.TryAddSingleton<ISmsGatewayResolver, SmsGatewayResolver>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, SmsBotProvider>());

        return builder;
    }
}
