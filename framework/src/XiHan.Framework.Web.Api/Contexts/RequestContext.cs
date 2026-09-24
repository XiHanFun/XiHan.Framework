// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Contexts;

/// <summary>
/// 请求上下文
/// </summary>
/// <remarks>
/// 管线最前端建立（跟踪、文化、客户端信息、起始时间）；身份字段（<see cref="UserId"/>、<see cref="UserName"/>、<see cref="TenantId"/>）
/// 要等认证与租户解析完成后才可知，由租户解析中间件定型写回。位于租户解析之前的中间件（访问日志、异常日志等）
/// 在 <c>await next()</c> 返回后读取到的，即是本次请求最终生效的身份。
/// </remarks>
public sealed record RequestContext
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// 请求文化（如 zh-CN / en-US），由请求文化中间件解析
    /// </summary>
    public string? Culture { get; init; }

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
    /// 租户标识（令牌或已验证的解析结果；null 表示宿主/平台）
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
