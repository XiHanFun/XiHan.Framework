// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.MultiBot;

/// <summary>
/// 机器人管理器运行状态（供应用层管理端点查询）
/// </summary>
public class BotManagerStatus
{
    /// <summary>
    /// 管理器是否已启动
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// 平台是否启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 传输模式（webhook / polling）
    /// </summary>
    public string TransportMode { get; set; } = string.Empty;

    /// <summary>
    /// 运行中机器人总数
    /// </summary>
    public int TotalBots { get; set; }

    /// <summary>
    /// 机器人运行信息列表
    /// </summary>
    public List<BotRunningInfo> Bots { get; set; } = [];
}

/// <summary>
/// 单个机器人运行信息
/// </summary>
public class BotRunningInfo
{
    /// <summary>
    /// 机器人名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 传输模式（webhook / polling）
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Telegram Bot 用户名（不含 @）
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Telegram Bot Id
    /// </summary>
    public long BotId { get; set; }

    /// <summary>
    /// 是否运行中
    /// </summary>
    public bool IsRunning { get; set; }
}
