// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 授权策略原语。
/// </summary>
/// <remarks>
/// 安全子系统的 RBAC / ABAC 引擎实现它；内核只定义判定接口。
/// </remarks>
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
    /// <remarks>
    /// 成功时是允许或拒绝的 <see cref="AuthorizationResult"/>；判定所依赖的存储或来源不可达、无法得出结论时返回失败（如 <c>SECURITY_UNAVAILABLE_001</c>），Authorize 阶段以该失败结束调用，不当作拒绝。
    /// </remarks>
    ValueTask<Result<AuthorizationResult>> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
