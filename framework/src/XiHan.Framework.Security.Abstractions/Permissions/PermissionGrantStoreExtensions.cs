// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予存储的单主体写法
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class PermissionGrantStoreExtensions
{
    /// <summary>
    /// 取一个主体在租户下的有效授予
    /// </summary>
    /// <remarks>
    /// 等于只含这个主体的 <see cref="IPermissionGrantStore.GetManyAsync"/>；要读多个主体时直接调批量方法，不要在循环里调它
    /// </remarks>
    public static async ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(this IPermissionGrantStore store, GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var read = await store.GetManyAsync([subject], tenantId, cancellationToken).ConfigureAwait(false);
        return read.Map(static grants => grants[0]);
    }
}
