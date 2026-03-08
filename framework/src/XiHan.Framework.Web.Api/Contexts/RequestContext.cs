#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RequestContext
// Guid:4c5f13f4-84a3-4f5c-9e4d-5e1f3a2e7c41
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Contexts;

/// <summary>
/// 请求上下文
/// </summary>
public sealed class RequestContext
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 请求标识
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; init; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; init; }

    /// <summary>
    /// 远端 IP
    /// </summary>
    public string? RemoteIp { get; init; }

    /// <summary>
    /// UserAgent
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// 请求开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;
}
