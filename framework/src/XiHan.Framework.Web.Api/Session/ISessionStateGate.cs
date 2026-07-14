#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISessionStateGate
// Guid:5c3a9f21-8d4e-4b67-9a02-1f7e6c3b8d45
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/15 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Session;

/// <summary>
/// 会话闸门判定结果
/// </summary>
public enum SessionGateStatus
{
    /// <summary>
    /// 放行
    /// </summary>
    Allow = 0,

    /// <summary>
    /// 会话已失效（登出/被踢下线/过期）——应以 401 拒绝，令客户端回到登录页
    /// </summary>
    Invalid = 1,

    /// <summary>
    /// 会话已锁屏——应以 423 拒绝，令客户端展示锁屏而<b>不是</b>登出（用户仍是本人）
    /// </summary>
    Locked = 2
}

/// <summary>
/// 会话闸门判定
/// </summary>
/// <param name="Status">判定结果</param>
/// <param name="DisplayName">锁屏时回传给前端的展示名（锁屏页要显示"是谁锁的"）</param>
/// <param name="AvatarUrl">锁屏时回传给前端的头像</param>
public sealed record SessionGateDecision(
    SessionGateStatus Status,
    string? DisplayName = null,
    string? AvatarUrl = null)
{
    /// <summary>
    /// 放行
    /// </summary>
    public static readonly SessionGateDecision Allow = new(SessionGateStatus.Allow);
}

/// <summary>
/// 会话状态闸门：由应用侧实现，中间件据此在鉴权前拦截失效/锁屏会话
/// </summary>
/// <remarks>
/// 框架不认识应用的会话模型（如 <c>SysUserSession</c>），故只暴露这层抽象；
/// 默认实现 <see cref="NullSessionStateGate"/> 一律放行，未接入会话体系的宿主零影响。
/// 应用侧用 <c>Replace</c> 注入真实现即可生效。
/// </remarks>
public interface ISessionStateGate
{
    /// <summary>
    /// 评估会话状态
    /// </summary>
    /// <param name="sessionId">JWT 中的会话标识（session_id claim）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>判定结果</returns>
    Task<SessionGateDecision> EvaluateAsync(string sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认会话闸门：一律放行
/// </summary>
public sealed class NullSessionStateGate : ISessionStateGate
{
    /// <inheritdoc />
    public Task<SessionGateDecision> EvaluateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SessionGateDecision.Allow);
    }
}
