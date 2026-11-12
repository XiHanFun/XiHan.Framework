// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Knowledge;

namespace XiHan.Framework.AI.Abstractions.Tests.Knowledge;

/// <summary>
/// 检索条数与文档过滤的上限（ADR-0190）
/// </summary>
/// <remarks>
/// 条数与文档 ID 常由调用方的输入而来：超过上限在构造时抛出，存储不会收到放大后的检索（条数乘以超取倍数会溢出，文档 ID 在向量存储里拼成过滤表达式）
/// </remarks>
public sealed class KnowledgeLimitTests
{
    /// <summary>
    /// 一维的查询向量
    /// </summary>
    private static readonly float[] _vector = [1f];

    [Fact]
    public void Top_IsBounded_InTheQueryAndTheSearch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new KnowledgeQuery { Text = "q", Top = KnowledgeSearch.MaxTop + 1 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new KnowledgeSearch(_vector, KnowledgeSearch.MaxTop + 1));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new KnowledgeOptions { Top = KnowledgeSearch.MaxTop + 1 });

        Assert.Equal(KnowledgeSearch.MaxTop, new KnowledgeQuery { Text = "q", Top = KnowledgeSearch.MaxTop }.Top);
        Assert.Equal(KnowledgeSearch.MaxTop, new KnowledgeSearch(_vector, KnowledgeSearch.MaxTop).Top);
    }

    [Fact]
    public void DocumentIds_AreBounded_AndCopied()
    {
        var tooMany = Enumerable.Range(0, RetrievalFilter.MaxDocumentIds + 1).Select(static i => $"doc-{i}").ToList();
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetrievalFilter { DocumentIds = tooMany });

        // 构造之后改原来的列表，过滤不跟着变：上限在构造时核对过，不能再被绕开
        var ids = new List<string> { "a" };
        var filter = new RetrievalFilter { DocumentIds = ids };
        ids.AddRange(tooMany);
        Assert.Equal(["a"], filter.DocumentIds);
    }
}
