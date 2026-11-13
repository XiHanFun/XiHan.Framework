// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Validation;
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
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(KnowledgeSearch.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
    /// </remarks>
    public const int Revision = 1;

    /// <summary>
    /// 这次查询用到的条件里最高的修订号
    /// </summary>
    /// <remarks>
    /// 没有用到新条件时不变，旧 Provider 照常服务。5.0 的条件都属于修订 1；以后新增条件时改为实例方法，按设置了的条件取其中最高的修订号，不要直接返回 <see cref="Revision"/>
    /// </remarks>
    private static int RequiredRevision() => 1;

    /// <summary>
    /// 核对这次用到的条件 Provider 是否都认识
    /// </summary>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/></param>
    /// <returns>成功；用到了更新的条件时为 <c>KERNEL_VALIDATION_007</c></returns>
    public Result Validate(int supportedRevision) => QueryRevision.Check(nameof(KnowledgeSearch), RequiredRevision(), supportedRevision);

    /// <summary>
    /// 一次检索最多返回的条数
    /// </summary>
    /// <remarks>
    /// 条数常由调用方的输入而来，存储会按它（带元数据过滤时乘以超取倍数）向后端取数（ADR-0190）
    /// </remarks>
    public const int MaxTop = 1000;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="vector">查询向量</param>
    /// <param name="top">最多返回的条数，1 到 <see cref="MaxTop"/></param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="top"/> 小于 1 或大于 <see cref="MaxTop"/></exception>
    public KnowledgeSearch(ReadOnlyMemory<float> vector, int top)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(top, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(top, MaxTop);
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
