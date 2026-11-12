// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 检索过滤：限定文档，或元数据键值相等（全部满足）
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RetrievalFilter
{
    /// <summary>
    /// 文档过滤最多的文档数
    /// </summary>
    /// <remarks>
    /// 文档 ID 常由调用方的输入而来，向量存储把它们拼成过滤表达式交给后端（ADR-0190）
    /// </remarks>
    public const int MaxDocumentIds = 1000;

    /// <summary>
    /// 只在这些文档里找
    /// </summary>
    /// <remarks>
    /// 空表示不限；最多 <see cref="MaxDocumentIds"/> 个，超过抛出 <see cref="ArgumentOutOfRangeException"/>。保存的是副本，之后改传入的列表不影响过滤
    /// </remarks>
    public IReadOnlyList<string>? DocumentIds
    {
        get => field;
        init => field = value is null
            ? null
            : value.Count <= MaxDocumentIds ? [.. value] : throw new ArgumentOutOfRangeException(nameof(value), value.Count, $"A filter may name at most {MaxDocumentIds} documents.");
    }

    /// <summary>
    /// 元数据必须等于这些键值
    /// </summary>
    /// <remarks>
    /// 空表示不限
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// 没有任何条件
    /// </summary>
    public bool IsEmpty => (DocumentIds is null || DocumentIds.Count == 0) && (Metadata is null || Metadata.Count == 0);

    /// <summary>
    /// 一条记录是否满足（供 Provider 与测试在进程内过滤）
    /// </summary>
    public bool Matches(string documentId, IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(metadata);
        if (DocumentIds is { Count: > 0 } && !DocumentIds.Contains(documentId, StringComparer.Ordinal))
        {
            return false;
        }

        if (Metadata is { Count: > 0 })
        {
            foreach (var (key, value) in Metadata)
            {
                if (!metadata.TryGetValue(key, out var actual) || !string.Equals(actual, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
