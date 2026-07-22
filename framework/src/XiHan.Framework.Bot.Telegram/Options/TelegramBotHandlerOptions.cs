// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 机器人处理器注册选项
/// </summary>
/// <remarks>
/// 处理器必须显式登记（推荐使用 AddTelegramBotHandler&lt;THandler&gt;() 扩展，
/// 会同时注册 DI 与登记本列表）；平台不做程序集反射扫描，未登记的处理器不会被路由。
/// </remarks>
public class TelegramBotHandlerOptions
{
    /// <summary>
    /// 处理器类型列表（须实现至少一个 IBot*Handler 接口）
    /// </summary>
    public List<Type> Handlers { get; } = [];
}
