// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 知识库存储（<c>ai.knowledge@1</c> 的 Provider 契约）
/// </summary>
/// <remarks>
/// 按集合写入记录、按文档删除、按块序号裁剪、按向量搜索。集合不存在：写入时创建，搜索返回空，删除与裁剪视为成功
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IKnowledgeStore
{
    /// <summary>
    /// 写入或替换记录（按 ID）
    /// </summary>
    /// <remarks>
    /// 不可达 → <see cref="AIErrors.KnowledgeUnavailable"/>
    /// </remarks>
    ValueTask<Result> UpsertAsync(string collection, IReadOnlyList<KnowledgeRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// 删除这些文档的全部记录
    /// </summary>
    /// <remarks>
    /// 不存在的文档忽略
    /// </remarks>
    ValueTask<Result> DeleteAsync(string collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken);

    /// <summary>
    /// 删除这些文档里多出来的块
    /// </summary>
    /// <remarks>
    /// 每项删掉块序号不小于 <see cref="KnowledgeTrim.ChunkCount"/> 的记录，更小的不动。重新摄入在新块写入成功之后调用它；文档不存在或没有多余的块时成功，空集合不访问后端。整批成功或失败，不可达 → <see cref="AIErrors.KnowledgeUnavailable"/>（ADR-0166）
    /// </remarks>
    ValueTask<Result> TrimAsync(string collection, IReadOnlyList<KnowledgeTrim> trims, CancellationToken cancellationToken);

    /// <summary>
    /// 按向量检索，按得分降序
    /// </summary>
    /// <remarks>
    /// 最多 <see cref="KnowledgeSearch.Top"/> 条；过滤与最低得分都由存储施加，原生支持的后端交给服务端（ADR-0166）
    /// </remarks>
    ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string collection, KnowledgeSearch search, CancellationToken cancellationToken);
}
