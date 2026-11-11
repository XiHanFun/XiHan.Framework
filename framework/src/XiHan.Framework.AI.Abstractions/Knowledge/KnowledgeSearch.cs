// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 一次向量检索的请求
/// </summary>
/// <remarks>
/// 向量与条数经构造给出，过滤与最低得分经初始化器给出；以后加检索参数只加属性，存储的签名不变（ADR-0166）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeSearch
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="vector">查询向量</param>
    /// <param name="top">最多返回的条数，至少 1</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="top"/> 小于 1</exception>
    public KnowledgeSearch(ReadOnlyMemory<float> vector, int top)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(top, 1);
        Vector = vector;
        Top = top;
    }

    /// <summary>
    /// 查询向量
    /// </summary>
    public ReadOnlyMemory<float> Vector { get; }

    /// <summary>
    /// 最多返回的条数
    /// </summary>
    public int Top { get; }

    /// <summary>
    /// 按文档与元数据过滤
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 不过滤
    /// </remarks>
    public RetrievalFilter? Filter { get; init; }

    /// <summary>
    /// 最低得分
    /// </summary>
    /// <remarks>
    /// 得分低于它的块不返回；由存储施加，原生支持得分阈值的后端交给服务端。为 <see langword="null"/> 不限
    /// </remarks>
    public double? MinScore { get; init; }
}
