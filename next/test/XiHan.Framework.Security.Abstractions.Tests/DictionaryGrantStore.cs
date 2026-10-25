// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests;

/// <summary>
/// 用固定授予数组实现的存储：返回主体的全局授予加该租户的授予。
/// </summary>
internal sealed class DictionaryGrantStore(params PermissionGrant[] grants) : IPermissionGrantStore
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyCollection<string>>(grants
            .Where(g => g.Subject == subject && (g.TenantId is null || g.TenantId == tenantId))
            .Select(static g => g.Permission)
            .ToList());
}
