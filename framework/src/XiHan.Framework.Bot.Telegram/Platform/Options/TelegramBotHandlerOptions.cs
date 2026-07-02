#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotHandlerOptions
// Guid:fede805b-6061-4264-b10e-a53414e69c55
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Platform.Options;

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
