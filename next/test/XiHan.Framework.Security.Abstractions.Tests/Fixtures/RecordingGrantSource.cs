// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录每次查询的授予来源：按主体 ID 给出固定的额外授予（没有登记的主体为空）。
/// </summary>
internal sealed class RecordingGrantSource(IReadOnlyDictionary<string, string[]> grants) : IPermissionGrantSource
{
    /// <summary>
    /// 被查询的主体 ID，按调用顺序。
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken)
    {
        Calls.Add(security.Principal.Id);
        return ValueTask.FromResult<IReadOnlyCollection<string>>(grants.TryGetValue(security.Principal.Id, out var granted) ? granted : []);
    }
}
