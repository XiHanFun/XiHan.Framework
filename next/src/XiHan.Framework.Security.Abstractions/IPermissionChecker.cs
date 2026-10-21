// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 判定主体是否持有权限。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionChecker
{
    /// <summary>
    /// 判定。
    /// </summary>
    ValueTask<bool> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken);
}
