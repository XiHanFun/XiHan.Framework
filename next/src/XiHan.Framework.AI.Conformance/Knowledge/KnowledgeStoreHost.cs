// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Conformance.Knowledge;

/// <summary>
/// 知识库存储套件的夹具：被测存储、它接受的向量维度，以及释放句柄。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeStoreHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建。
    /// </summary>
    public KnowledgeStoreHost(IKnowledgeStore store, int dimensions, IAsyncDisposable host)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        Dimensions = dimensions;
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 被测存储。
    /// </summary>
    public IKnowledgeStore Store { get; }

    /// <summary>
    /// 向量维度。
    /// </summary>
    public int Dimensions { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
