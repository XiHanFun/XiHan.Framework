// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录每次调用的收窄过滤器：只放行 <see cref="Allowed"/> 里的权限码（为 <see langword="null"/> 时全部放行）。
/// </summary>
internal sealed class RecordingPermissionFilter(IReadOnlySet<string>? allowed) : IPermissionFilter
{
    /// <summary>
    /// 放行的权限码；<see langword="null"/> 表示全部放行。
    /// </summary>
    public IReadOnlySet<string>? Allowed { get; } = allowed;

    /// <summary>
    /// 收到的调用（主体 ID，权限码），按调用顺序。
    /// </summary>
    public List<(string Principal, string Permission)> Calls { get; } = [];

    /// <inheritdoc />
    public ValueTask<bool> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
        Calls.Add((security.Principal.Id, permission));
        return ValueTask.FromResult(Allowed is null || Allowed.Contains(permission));
    }
}
