// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Security.Abstractions.Tests;

internal sealed class DictionaryGrantStore(params PermissionGrant[] grants) : IPermissionGrantStore
{
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyCollection<string>>(grants
            .Where(g => g.Subject == subject && (g.TenantId is null || g.TenantId == tenantId))
            .Select(static g => g.Permission)
            .ToList());
}
