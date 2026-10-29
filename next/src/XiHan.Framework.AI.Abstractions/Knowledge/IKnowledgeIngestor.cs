// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 摄入。
/// </summary>
/// <remarks>
/// 切块 → 嵌入 → 写入存储。没有 <c>ai.knowledge</c> Provider → <see cref="AIErrors.KnowledgeUnavailable"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IKnowledgeIngestor
{
    /// <summary>
    /// 摄入多篇文档。
    /// </summary>
    /// <remarks>
    /// 逐项结果是该文档的块数（空文本为 0，且清掉旧块）。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<int>>> IngestAsync(IReadOnlyList<KnowledgeIngestRequest> requests, CancellationToken cancellationToken);

    /// <summary>
    /// 删除文档。
    /// </summary>
    /// <remarks>
    /// <paramref name="collection"/> 为空取默认集合。
    /// </remarks>
    ValueTask<Result> RemoveAsync(string? collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken);
}
