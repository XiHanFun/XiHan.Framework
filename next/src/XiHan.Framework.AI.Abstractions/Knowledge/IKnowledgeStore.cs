// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 知识库存储（<c>ai.knowledge@1</c> 的 Provider 契约）。
/// </summary>
/// <remarks>
/// 按集合写入记录、按文档删除、按向量搜索。集合不存在：写入时创建，搜索返回空。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IKnowledgeStore
{
    /// <summary>
    /// 写入或替换记录（按 ID）。
    /// </summary>
    /// <remarks>
    /// 不可达 → <see cref="AIErrors.KnowledgeUnavailable"/>。
    /// </remarks>
    ValueTask<Result> UpsertAsync(string collection, IReadOnlyList<KnowledgeRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// 删除这些文档的全部记录。
    /// </summary>
    /// <remarks>
    /// 不存在的文档忽略。
    /// </remarks>
    ValueTask<Result> DeleteAsync(string collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken);

    /// <summary>
    /// 按向量搜索，最多 <paramref name="top"/> 条，按得分降序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string collection, ReadOnlyMemory<float> vector, int top, RetrievalFilter? filter, CancellationToken cancellationToken);
}
