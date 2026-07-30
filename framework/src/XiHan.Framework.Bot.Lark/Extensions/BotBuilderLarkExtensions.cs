// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Stores;

namespace XiHan.Framework.Bot.Lark.Extensions;

/// <summary>
/// BotBuilder 飞书扩展
/// </summary>
public static class BotBuilderLarkExtensions
{
    /// <summary>
    /// 启用飞书提供者
    /// </summary>
    /// <param name="builder">Bot 构建器</param>
    /// <param name="configure">飞书配置委托</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseLark(this BotBuilder builder, Action<LarkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, LarkBotProvider>());
        builder.Services.TryAddSingleton<ILarkConfigStore, DefaultLarkConfigStore>();

        return builder;
    }
}
