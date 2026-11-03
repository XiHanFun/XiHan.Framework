// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 用固定授予数组实现的存储：返回主体的全局授予加该租户的授予
/// </summary>
/// <remarks>
/// 设置 <see cref="Failure"/> 后每次读取都返回该失败，模拟存储不可达
/// </remarks>
internal sealed class DictionaryGrantStore(params PermissionGrant[] grants) : IPermissionGrantStore
{
    /// <summary>
    /// 不为 <see langword="null"/> 时读取返回的失败
    /// </summary>
    public Error? Failure { get; set; }

    /// <summary>
    /// 被读取的主体，按调用顺序
    /// </summary>
    public List<GrantSubject> Calls { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        Calls.Add(subject);
        return ValueTask.FromResult(Failure is { } failure
            ? Result.Failure<IReadOnlyCollection<string>>(failure)
            : Result.Success<IReadOnlyCollection<string>>(grants
                .Where(g => g.Subject == subject && (g.TenantId is null || g.TenantId == tenantId))
                .Select(static g => g.Permission)
                .ToList()));
    }
}
