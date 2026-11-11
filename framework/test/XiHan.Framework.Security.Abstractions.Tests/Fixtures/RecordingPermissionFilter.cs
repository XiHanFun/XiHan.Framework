// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录每次调用的收窄过滤器：只放行 <see cref="Allowed"/> 里的权限码（为 <see langword="null"/> 时全部放行）
/// </summary>
/// <remarks>
/// 设置 <see cref="Failure"/> 后每次判定都返回该失败，模拟过滤器依赖的数据读不到
/// </remarks>
internal sealed class RecordingPermissionFilter(IReadOnlySet<string>? allowed) : IPermissionFilter
{
    /// <summary>
    /// 放行的权限码
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示全部放行
    /// </remarks>
    public IReadOnlySet<string>? Allowed { get; } = allowed;

    /// <summary>
    /// 收到的调用（主体 ID，权限码），按调用顺序
    /// </summary>
    public List<(string Principal, string Permission)> Calls { get; } = [];

    /// <summary>
    /// 不为 <see langword="null"/> 时判定返回的失败
    /// </summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<bool>> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
        // 检查器只为已认证主体调用过滤器，ID 一定有值
        Calls.Add((security.Principal.Id ?? throw new InvalidOperationException("Permission filters are only called for authenticated principals."), permission));
        return ValueTask.FromResult(Failure is { } failure ? Result.Failure<bool>(failure) : Result.Success(Allowed is null || Allowed.Contains(permission)));
    }
}
