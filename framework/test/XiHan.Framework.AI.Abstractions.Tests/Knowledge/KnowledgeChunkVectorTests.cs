// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Knowledge;

/// <summary>
/// 回放 <c>knowledge-chunks.json</c>：知识库的分块、记录 ID 与默认集合
/// </summary>
/// <remarks>
/// 规范 RFC-0022「知识库」（ADR-0210、ADR-0277）：固定窗口分块先把换行统一成 LF 再按窗口滑动，全是空白的块丢弃；记录 ID 是文档 ID 加 <c>#</c> 加块序号；请求没有指明集合时，不带租户的调用用配置的集合，带租户的调用用集合加 <c>.</c> 加租户 ID，拼出的名字不合文法时调用以校验错误结束。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class KnowledgeChunkVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly KnowledgeVectors _vectors = ConformanceVectors.Load<KnowledgeVectors>("knowledge-chunks.json");

    [Fact]
    public void Defaults_AndLimits_AreTheOnesInTheVectors()
    {
        var options = new KnowledgeOptions();
        Assert.Equal(KnowledgeOptions.DefaultCollection, _vectors.Defaults.Collection);
        Assert.Equal(_vectors.Defaults.Collection, options.Collection);
        Assert.Equal(_vectors.Defaults.Dimensions, options.Dimensions);
        Assert.Equal(_vectors.Defaults.ChunkSize, options.ChunkSize);
        Assert.Equal(_vectors.Defaults.ChunkOverlap, options.ChunkOverlap);
        Assert.Equal(_vectors.Defaults.Top, options.Top);

        // 分块选项单独用时的缺省值与知识库选项给出的一致
        Assert.Equal(new ChunkingOptions(), options.Chunking);
        Assert.Equal((_vectors.Defaults.ChunkSize, _vectors.Defaults.ChunkOverlap), (options.Chunking.MaxChunkSize, options.Chunking.Overlap));

        Assert.Equal(KnowledgeSearch.MaxTop, _vectors.MaxTop);
        Assert.Equal(RetrievalFilter.MaxDocumentIds, _vectors.MaxDocumentIds);
        Assert.Equal(AIErrors.RequestInvalid, _vectors.Code);
    }

    [Fact]
    public void FixedWindow_CutsTheChunksOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Chunks);
        var strategy = new FixedWindowChunkingStrategy();
        var failures = new List<string>();
        foreach (var vector in _vectors.Chunks)
        {
            var chunks = strategy.Chunk(vector.Text, new ChunkingOptions { MaxChunkSize = vector.MaxChunkSize, Overlap = vector.Overlap });
            if (!chunks.SequenceEqual(vector.Chunks, StringComparer.Ordinal))
            {
                failures.Add($"{vector.Name}: cut [{Describe(chunks)}], the vector has [{Describe(vector.Chunks)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void IdOf_GivesTheRecordIdOfEveryCase()
    {
        Assert.NotEmpty(_vectors.RecordIds);
        Assert.All(_vectors.RecordIds, static vector => Assert.Equal(vector.Id, KnowledgeRecord.IdOf(vector.DocumentId, vector.ChunkIndex)));
    }

    [Fact]
    public void DefaultCollectionFor_GivesTheCollectionOfEveryCase()
    {
        Assert.NotEmpty(_vectors.DefaultCollections);
        var failures = new List<string>();
        foreach (var vector in _vectors.DefaultCollections)
        {
            var options = new KnowledgeOptions { Collection = vector.Collection };
            var derived = options.DefaultCollectionFor(vector.Tenant).TryGetValue(out var collection) ? collection : null;
            if (derived != vector.DefaultCollection)
            {
                failures.Add($"{vector.Name}: derived '{derived ?? "none"}', the vector has '{vector.DefaultCollection ?? "none"}'");
            }

            // 摄入与检索共用的那一步：租户取自这次调用的安全上下文，派生不出合法的名字时以向量给的错误码结束
            var context = TestInvocationContext.Create();
            context.SetSecurity(new SecurityContext { Principal = Principal.Anonymous, Tenant = vector.Tenant is { } tenant ? new TenantContext { Id = tenant } : null });
            var resolved = KnowledgeErrors.CollectionFor(requested: null, options, new FixedInvocation(context));
            var outcome = resolved.TryGetValue(out var used) ? used : resolved.Error?.Code;
            if (outcome != (vector.DefaultCollection ?? _vectors.Code))
            {
                failures.Add($"{vector.Name}: the call used or failed with '{outcome}', the vector has '{vector.DefaultCollection ?? _vectors.Code}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheWindowEdgesAndTheNameLimit()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过。分块：重叠不小于窗口（要裁）、两种要统一的换行、被丢弃的空白块、一块都没有
        Assert.Contains(_vectors.Chunks, static vector => vector.Overlap >= vector.MaxChunkSize);
        Assert.Contains(_vectors.Chunks, static vector => vector.Overlap > 0 && vector.Overlap < vector.MaxChunkSize && vector.Chunks.Length > 1);
        Assert.Contains(_vectors.Chunks, static vector => vector.Text.Contains("\r\n", StringComparison.Ordinal));
        Assert.Contains(_vectors.Chunks, static vector => vector.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\r'));
        Assert.Contains(_vectors.Chunks, static vector => vector.Chunks.Length > 0 && vector.Chunks.Sum(static chunk => chunk.Length) < vector.Text.Length && vector.Overlap == 0 && !vector.Text.Contains('\r'));
        Assert.Contains(_vectors.Chunks, static vector => vector.Text.Length > 0 && vector.Chunks.Length == 0);
        Assert.Contains(_vectors.Chunks, static vector => vector.Text.Length == 0);

        // 默认集合：不带租户、拼出来正好到上限、只多一个字符
        Assert.Contains(_vectors.DefaultCollections, static vector => vector.Tenant is null && vector.DefaultCollection == vector.Collection);
        Assert.Contains(_vectors.DefaultCollections, static vector => vector.DefaultCollection is { Length: AIIdentifiers.MaxNameLength });
        Assert.Contains(_vectors.DefaultCollections, static vector => vector.DefaultCollection is null && $"{vector.Collection}.{vector.Tenant}".Length == AIIdentifiers.MaxNameLength + 1);
    }

    /// <summary>
    /// 失败信息里的若干块
    /// </summary>
    /// <remarks>
    /// 各自加引号、换行写成转义：块的边界与块里的换行才看得出来
    /// </remarks>
    private static string Describe(IEnumerable<string> chunks)
        => string.Join(", ", chunks.Select(static chunk => $"'{chunk.Replace("\n", "\\n", StringComparison.Ordinal)}'"));

    /// <summary>
    /// 固定返回给定上下文的调用访问器
    /// </summary>
    /// <param name="context">当前调用的上下文</param>
    private sealed class FixedInvocation(InvocationContext context) : IInvocationAccessor
    {
        /// <inheritdoc />
        public Maybe<InvocationContext> Current => Maybe.Some(context);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Defaults">知识库选项的缺省值</param>
    /// <param name="MaxTop">检索条数的上限</param>
    /// <param name="MaxDocumentIds">文档过滤里文档 ID 的个数上限</param>
    /// <param name="Code">派生不出默认集合时调用的错误码</param>
    /// <param name="Chunks">固定窗口分块的用例</param>
    /// <param name="RecordIds">记录 ID 的用例</param>
    /// <param name="DefaultCollections">默认集合的用例</param>
    private sealed record KnowledgeVectors(KnowledgeDefaults Defaults, int MaxTop, int MaxDocumentIds, string Code, ChunkCase[] Chunks, RecordIdCase[] RecordIds, DefaultCollectionCase[] DefaultCollections);

    /// <summary>
    /// 知识库选项的缺省值
    /// </summary>
    /// <param name="Collection">默认集合</param>
    /// <param name="Dimensions">嵌入维度</param>
    /// <param name="ChunkSize">切块大小</param>
    /// <param name="ChunkOverlap">相邻块的重叠</param>
    /// <param name="Top">默认检索条数</param>
    private sealed record KnowledgeDefaults(string Collection, int Dimensions, int ChunkSize, int ChunkOverlap, int Top);

    /// <summary>
    /// 一个分块用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Text">文本</param>
    /// <param name="MaxChunkSize">窗口的字符数</param>
    /// <param name="Overlap">相邻两块重叠的字符数</param>
    /// <param name="Chunks">切出的块</param>
    private sealed record ChunkCase(string Name, string Text, int MaxChunkSize, int Overlap, string[] Chunks);

    /// <summary>
    /// 一个记录 ID 用例
    /// </summary>
    /// <param name="DocumentId">文档 ID</param>
    /// <param name="ChunkIndex">块序号</param>
    /// <param name="Id">记录 ID</param>
    private sealed record RecordIdCase(string DocumentId, int ChunkIndex, string Id);

    /// <summary>
    /// 一个默认集合用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Collection">配置的集合</param>
    /// <param name="Tenant">调用的租户；<see langword="null"/> 表示不带租户</param>
    /// <param name="DefaultCollection">请求没有指明集合时用的集合；<see langword="null"/> 表示派生不出、要显式指明</param>
    private sealed record DefaultCollectionCase(string Name, string Collection, string? Tenant, string? DefaultCollection);
}
