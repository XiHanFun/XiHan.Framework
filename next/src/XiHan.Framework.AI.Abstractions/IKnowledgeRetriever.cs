// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 检索：问题嵌入 → 向量搜索 → 按最低得分过滤。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IKnowledgeRetriever
{
    /// <summary>
    /// 检索。
    /// </summary>
    ValueTask<Result<IReadOnlyList<RetrievedChunk>>> RetrieveAsync(KnowledgeQuery query, CancellationToken cancellationToken);
}
