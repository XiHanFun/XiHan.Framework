#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramHandlerResolver
// Guid:ccbb35c2-a1dd-4855-82d9-03bcf88e15f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Bot.Telegram.Platform.Routing;

/// <summary>
/// 处理器实例解析辅助（从作用域提供者按目录类型列表实例化并排序）
/// </summary>
internal static class TelegramHandlerResolver
{
    /// <summary>
    /// 解析并按 Order 排序处理器实例（未注册 DI 的类型记录错误日志并跳过）
    /// </summary>
    /// <typeparam name="THandler">处理器接口类型</typeparam>
    /// <param name="provider">作用域服务提供者</param>
    /// <param name="handlerTypes">处理器类型列表</param>
    /// <param name="orderSelector">排序键选择器</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>排序后的处理器实例列表</returns>
    internal static List<THandler> ResolveOrdered<THandler>(
        IServiceProvider provider,
        IReadOnlyList<Type> handlerTypes,
        Func<THandler, int> orderSelector,
        ILogger logger)
        where THandler : class
    {
        var handlers = new List<THandler>(handlerTypes.Count);
        foreach (var handlerType in handlerTypes)
        {
            if (provider.GetService(handlerType) is THandler handler)
            {
                handlers.Add(handler);
            }
            else
            {
                logger.LogError(
                    "Telegram 处理器未在 DI 注册：{HandlerType}（请使用 AddTelegramBotHandler<THandler>() 注册）。",
                    handlerType.FullName);
            }
        }

        return [.. handlers.OrderBy(orderSelector)];
    }
}
