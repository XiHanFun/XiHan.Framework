// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 一次授权请求：谁（安全上下文）对什么（资源）做什么（动作）。
/// </summary>
/// <param name="Security">安全上下文。</param>
/// <param name="Action">动作，通常是操作 ID。</param>
/// <param name="Resource">资源名；无具体资源时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequest(SecurityContext Security, string Action, ResourceName? Resource = null)
{
    /// <summary>
    /// 安全上下文。
    /// </summary>
    public SecurityContext Security { get; } = Security ?? throw new ArgumentNullException(nameof(Security));

    /// <summary>
    /// 动作。
    /// </summary>
    public string Action { get; } = string.IsNullOrWhiteSpace(Action) ? throw new ArgumentException("动作不能为空。", nameof(Action)) : Action;
}
