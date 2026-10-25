// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 单篇文档的便捷重载（批量方法的特例）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class KnowledgeIngestorExtensions
{
    /// <summary>
    /// 摄入一篇文档，返回块数。
    /// </summary>
    public static async ValueTask<Result<int>> IngestAsync(this IKnowledgeIngestor ingestor, KnowledgeIngestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        ArgumentNullException.ThrowIfNull(request);
        var results = await ingestor.IngestAsync([request], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 删除一篇文档。
    /// </summary>
    public static ValueTask<Result> RemoveAsync(this IKnowledgeIngestor ingestor, string? collection, string documentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        ArgumentNullException.ThrowIfNull(documentId);
        return ingestor.RemoveAsync(collection, [documentId], cancellationToken);
    }
}
