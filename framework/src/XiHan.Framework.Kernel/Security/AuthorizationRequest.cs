// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 一次授权请求：谁（安全上下文）对什么（资源）做什么（动作）
/// </summary>
/// <remarks>
/// 主体与动作经构造给出，其余成员经初始化器给出，以后加字段只加属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequest
{
    /// <summary>
    /// 创建授权请求
    /// </summary>
    /// <param name="security">安全上下文</param>
    /// <param name="action">动作，通常是操作 ID</param>
    /// <exception cref="ArgumentNullException"><paramref name="security"/> 为 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 为空或空白</exception>
    public AuthorizationRequest(SecurityContext security, string action)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Security = security;
        Action = action;
    }

    /// <summary>
    /// 安全上下文
    /// </summary>
    public SecurityContext Security { get; }

    /// <summary>
    /// 动作
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// 资源名
    /// </summary>
    /// <remarks>
    /// 无具体资源时为 <see langword="null"/>
    /// </remarks>
    public ResourceName? Resource { get; init; }
}
