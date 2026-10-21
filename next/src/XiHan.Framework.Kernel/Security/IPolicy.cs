// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 授权策略原语。安全子系统的 RBAC / ABAC 引擎实现它；内核只定义判定接口。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPolicy
{
    /// <summary>
    /// 策略名。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 判定。
    /// </summary>
    ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
