// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 判定主体是否持有权限。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionChecker
{
    /// <summary>
    /// 判定。
    /// </summary>
    /// <remarks>
    /// 成功的 <see langword="true"/> / <see langword="false"/> 是持有 / 不持有；授予存储、授予来源或收窄过滤器失败时原样返回该失败（如 <see cref="SecurityErrors.StoreUnavailable"/>），调用方不得把失败当作不持有。
    /// </remarks>
    ValueTask<Result<bool>> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken);
}
