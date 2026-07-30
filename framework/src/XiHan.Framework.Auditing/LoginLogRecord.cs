// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 登录日志记录模型
/// </summary>
public class LoginLogRecord
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 会话标识
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 登录结果（由应用层定义语义，0=成功，其他为各类失败）
    /// </summary>
    public int LoginResult { get; set; }

    /// <summary>
    /// 登录消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 登录IP
    /// </summary>
    public string? LoginIp { get; set; }

    /// <summary>
    /// User-Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 设备标识
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// 登录时间
    /// </summary>
    public DateTimeOffset LoginTime { get; set; }
}
