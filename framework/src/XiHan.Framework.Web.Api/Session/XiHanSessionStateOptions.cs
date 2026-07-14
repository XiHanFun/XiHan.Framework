#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSessionStateOptions
// Guid:9e1b7d34-2f65-4a08-b3c9-6d0e4f8a2b71
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/15 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Session;

/// <summary>
/// 会话状态中间件选项
/// </summary>
public sealed class XiHanSessionStateOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:SessionState";

    /// <summary>
    /// 是否启用会话状态闸门（关闭后中间件直接放行）
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 锁屏时仍需放行的路径（不区分大小写的前缀匹配）
    /// </summary>
    /// <remarks>
    /// 必须放行：解锁端点（否则永远解不开）、登出端点（锁屏中必须能登出）、
    /// 刷新令牌（长时间锁屏会跨越 access token 有效期，挡掉会把锁屏打成登出；
    /// 刷新保留同一 session_id，锁屏位不会因此丢失）。
    /// <para>注意：这些路径只对<b>锁屏(423)</b>放行；会话失效(401) 一律拦截，不受此列表影响。</para>
    /// </remarks>
    public List<string> LockAllowedPaths { get; set; } = [];

    /// <summary>
    /// SignalR Hub 路径前缀：命中则整体跳过本中间件
    /// </summary>
    /// <remarks>
    /// Hub 长连接不能直接 423/401——客户端会陷入重连死循环，且连接本身还要用来接收
    /// "被踢下线/已解锁"的推送。Hub 的方法级拦截由 <c>IHubFilter</c> 承担。
    /// </remarks>
    public string SignalRHubPathPrefix { get; set; } = "/hubs";
}
