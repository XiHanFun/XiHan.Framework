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
    /// 每次读取的主体集合，按调用顺序
    /// </summary>
    public List<GrantSubject[]> Calls { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<IReadOnlyCollection<string>>>> GetManyAsync(IReadOnlyList<GrantSubject> subjects, string? tenantId, CancellationToken cancellationToken)
    {
        Calls.Add([.. subjects]);
        return ValueTask.FromResult(Failure is { } failure
            ? Result.Failure<IReadOnlyList<IReadOnlyCollection<string>>>(failure)
            : Result.Success<IReadOnlyList<IReadOnlyCollection<string>>>(subjects
                .Select(subject => (IReadOnlyCollection<string>)grants
                    .Where(g => g.Subject == subject && PermissionGrant.VisibleScopes(tenantId).Contains(g.TenantId))
                    .Select(static g => g.Permission)
                    .ToList())
                .ToList()));
    }
}
