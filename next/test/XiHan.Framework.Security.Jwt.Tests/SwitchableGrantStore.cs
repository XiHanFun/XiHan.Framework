// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// 可切换为不可达的授予存储：可达时给 <see cref="Granted"/> 里的主体 ID 授予 <see cref="Permission"/>。
/// </summary>
internal sealed class SwitchableGrantStore : IPermissionGrantStore
{
    /// <summary>
    /// 授予的权限码。
    /// </summary>
    public const string Permission = "orders.read";

    /// <summary>
    /// 持有 <see cref="Permission"/> 的用户 ID。
    /// </summary>
    public HashSet<string> Granted { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 为真时每次读取返回 <see cref="SecurityErrors.StoreUnavailable"/>。
    /// </summary>
    public bool Unavailable { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyCollection<string>>(Error.Unavailable(SecurityErrors.StoreUnavailable, SecurityMessages.GrantStoreUnavailable)));
        }

        IReadOnlyCollection<string> grants = subject.Kind == GrantSubject.User && Granted.Contains(subject.Id) ? [Permission] : [];
        return ValueTask.FromResult(Result.Success(grants));
    }
}
