// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 检索过滤：限定文档，或元数据键值相等（全部满足）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RetrievalFilter
{
    /// <summary>
    /// 只在这些文档里找；空表示不限。
    /// </summary>
    public IReadOnlyList<string>? DocumentIds { get; init; }

    /// <summary>
    /// 元数据必须等于这些键值；空表示不限。
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// 没有任何条件。
    /// </summary>
    public bool IsEmpty => (DocumentIds is null || DocumentIds.Count == 0) && (Metadata is null || Metadata.Count == 0);

    /// <summary>
    /// 一条记录是否满足（供 Provider 与测试在进程内过滤）。
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
