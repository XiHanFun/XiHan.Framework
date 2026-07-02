#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilderLarkExtensions
// Guid:bb417652-c480-4463-8276-024dad56f599
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;

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
