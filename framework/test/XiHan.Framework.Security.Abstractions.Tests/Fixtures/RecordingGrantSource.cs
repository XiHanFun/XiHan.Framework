// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录每次查询的授予来源：按主体 ID 给出固定的额外授予（没有登记的主体为空）
/// </summary>
/// <remarks>
/// 设置 <see cref="Failure"/> 后每次查询都返回该失败，模拟来源依赖的数据读不到
/// </remarks>
internal sealed class RecordingGrantSource(IReadOnlyDictionary<string, string[]> grants) : IPermissionGrantSource
{
    /// <summary>
    /// 被查询的主体 ID，按调用顺序
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// 不为 <see langword="null"/> 时查询返回的失败
    /// </summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken)
    {
        // 检查器只为已认证主体调用来源，ID 一定有值
        var principalId = security.Principal.Id ?? throw new InvalidOperationException("Grant sources are only called for authenticated principals.");
        Calls.Add(principalId);
        return ValueTask.FromResult(Failure is { } failure
            ? Result.Failure<IReadOnlyCollection<string>>(failure)
            : Result.Success<IReadOnlyCollection<string>>(grants.TryGetValue(principalId, out var granted) ? granted : []));
    }
}
