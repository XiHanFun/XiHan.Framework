// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Core.Access;

/// <summary>
/// 请求访问闸门：由应用侧实现，在认证与租户解析之后、会话闸门与授权之前判定是否放行这次请求
/// </summary>
/// <remarks>
/// 适合按 IP、地区、时段、用户、角色、接口等属性拦截请求（访问策略、维护模式、封禁名单）。
/// 所有请求都会经过，包括匿名接口、Minimal API 与实时通道的协商请求；此时用户与租户已经可用（匿名请求没有用户）。
/// 可注册多个，按 <see cref="Order"/> 从小到大依次判定，第一个拒绝即短路；全部放行才继续。
/// 闸门自己决定出错时是放行还是拒绝，中间件不吞异常。
/// </remarks>
public interface IRequestAccessGate
{
    /// <summary>
    /// 判定顺序，越小越先
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 判定当前请求
    /// </summary>
    /// <param name="context">当前请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>判定结果</returns>
    ValueTask<RequestAccessDecision> EvaluateAsync(HttpContext context, CancellationToken cancellationToken = default);
}
