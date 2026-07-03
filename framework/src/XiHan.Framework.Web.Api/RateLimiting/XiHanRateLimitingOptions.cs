#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRateLimitingOptions
// Guid:a7b8c9d0-e1f2-4a64-bf7a-8b9c0d1e2f3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/01 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.RateLimiting;

/// <summary>
/// 入站限流选项
/// </summary>
/// <remarks>
/// 基于 ASP.NET Core 内置 RateLimiter，按客户端 IP 固定窗口全局限流。默认关闭（<see cref="IsEnabled"/>=false），
/// 开启后超额请求返回 429 并带 Retry-After。反向代理转发头已在管线最前还原，故 IP 为真实客户端 IP。
/// </remarks>
public class XiHanRateLimitingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string SectionName = "XiHan:Web:RateLimiting";

    /// <summary>
    /// 是否启用（默认关闭）
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 每窗口允许的请求数（默认 300）
    /// </summary>
    public int PermitLimit { get; set; } = 300;

    /// <summary>
    /// 窗口秒数（默认 60）
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// 排队上限（默认 0：超额立即拒绝，不排队）
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// 豁免路径前缀（默认放行 /health 探活）
    /// </summary>
    public string[] ExemptPathPrefixes { get; set; } = ["/health"];
}
