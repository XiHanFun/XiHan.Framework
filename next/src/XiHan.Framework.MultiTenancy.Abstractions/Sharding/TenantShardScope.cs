// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 管道外给服务作用域绑定租户分片。
/// </summary>
/// <remarks>
/// 作用域服务（RFC-0007，ADR-0057）。后台写入、作业与租户库初始化自己建作用域、不经 <c>App.InvokeAsync</c>，在首次创建持久化客户端之前绑定，客户端就带上分片连接；不看租户是否启用，也不受应用是否在服务状态影响。显式绑定优先于调用上下文里的分片。工作单元的租户仍由调用方给出，应与绑定的租户一致。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TenantShardScope
{
    /// <summary>
    /// 分片解析器。
    /// </summary>
    /// <remarks>
    /// 没有装配分片能力时为 <see langword="null"/>。
    /// </remarks>
    private readonly ITenantShardResolver? _resolver;

    /// <summary>
    /// 创建。
    /// </summary>
    public TenantShardScope(ITenantShardResolver? resolver = null)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// 显式绑定的分片。
    /// </summary>
    /// <remarks>
    /// 没有绑定为无值。
    /// </remarks>
    public Maybe<TenantShard> Shard { get; private set; }

    /// <summary>
    /// 作用域的持久化客户端是否已经取过连接。
    /// </summary>
    /// <remarks>
    /// 取过之后不能再绑定。
    /// </remarks>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 绑定一个分片。
    /// </summary>
    /// <remarks>
    /// 客户端已经取过连接、或已绑定另一个分片时抛出 <see cref="InvalidOperationException"/>；同一个分片重复绑定无副作用。
    /// </remarks>
    public void Bind(TenantShard shard)
    {
        ArgumentNullException.ThrowIfNull(shard);
        if (Shard.TryGetValue(out var bound))
        {
            if (SameConnection(bound, shard))
            {
                return;
            }

            throw new InvalidOperationException($"The scope is already bound to the shard of tenant '{bound.TenantId}' and cannot be rebound to tenant '{shard.TenantId}'.");
        }

        if (IsConnected)
        {
            throw new InvalidOperationException($"The scope's persistence client was already created; bind the shard of tenant '{shard.TenantId}' before resolving persistence services.");
        }

        Shard = Maybe.Some(shard);
    }

    /// <summary>
    /// 按租户 ID 解析分片并绑定。
    /// </summary>
    /// <remarks>
    /// 返回解析出的分片：共库租户或没有装配分片能力时成功且无值、不绑定；解析失败返回错误、不绑定。不看租户是否启用。
    /// </remarks>
    public async ValueTask<Result<Maybe<TenantShard>>> BindTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (_resolver is null)
        {
            return Result.Success(Maybe.None<TenantShard>());
        }

        var resolved = await _resolver.ResolveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (resolved.TryGetValue(out var found) && found.TryGetValue(out var shard))
        {
            Bind(shard);
        }

        return resolved;
    }

    /// <summary>
    /// 记下客户端已经取过连接。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="TenantShardBinding.Resolve"/> 在持久化 Provider 创建客户端时调用。
    /// </remarks>
    internal void MarkConnected() => IsConnected = true;

    /// <summary>
    /// 两个分片是否指向同一个连接。
    /// </summary>
    private static bool SameConnection(TenantShard left, TenantShard right)
        => string.Equals(left.TenantId, right.TenantId, StringComparison.Ordinal)
            && string.Equals(left.ConnectionString, right.ConnectionString, StringComparison.Ordinal)
            && string.Equals(left.Provider, right.Provider, StringComparison.Ordinal);
}
