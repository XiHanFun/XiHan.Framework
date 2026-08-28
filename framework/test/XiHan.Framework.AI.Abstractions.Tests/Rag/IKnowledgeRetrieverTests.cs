// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Tests.Rag;

/// <summary>
/// 知识检索器契约测试
/// </summary>
/// <remarks>
/// topK 的默认值 5 是本接口唯一带具体数值的约定，且它被编译进每一个省略该参数的调用点：
/// 改动不会让任何代码编译失败，只会让线上召回条数悄悄变化，进而改变提示词长度与回答质量。
/// 同理，filter 默认 null 表示不限租户——这一条在多租户下是安全边界，必须显式确认。
/// </remarks>
public class IKnowledgeRetrieverTests
{
    /// <summary>
    /// 省略 topK 时实现侧收到 5
    /// </summary>
    /// <remarks>
    /// 刻意经接口引用调用：可选参数的默认值在调用点按「编译期已知的接收者类型」取，
    /// 若用具体类引用调用，取到的会是替身自己声明的默认值，这条断言就失去意义。
    /// </remarks>
    [Fact]
    public async Task RetrieveAsync_WhenTopKOmitted_UsesFiveAsDefault()
    {
        var fake = new RecordingKnowledgeRetriever();
        IKnowledgeRetriever retriever = fake;

        await retriever.RetrieveAsync("曦寒框架是什么", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(5, fake.LastTopK);
    }

    /// <summary>
    /// 省略过滤条件时实现侧收到 null，即不限租户与文档
    /// </summary>
    /// <remarks>
    /// 这一条刻意与「默认限定当前租户」区分：抽象层不隐式加租户过滤，
    /// 隔离必须由调用方显式传入 filter，避免上层误以为框架已经兜住了越权。
    /// </remarks>
    [Fact]
    public async Task RetrieveAsync_WhenFilterOmitted_PassesNull()
    {
        var fake = new RecordingKnowledgeRetriever();
        IKnowledgeRetriever retriever = fake;

        await retriever.RetrieveAsync("查询", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(fake.LastFilter);
        Assert.Null(fake.LastProvider);
    }

    /// <summary>
    /// 显式传入的过滤条件与 provider 原样抵达实现侧
    /// </summary>
    [Fact]
    public async Task RetrieveAsync_WithFilterAndProvider_PassesThemVerbatim()
    {
        var fake = new RecordingKnowledgeRetriever();
        IKnowledgeRetriever retriever = fake;
        var filter = new RetrievalFilter
        {
            TenantId = 1024,
            DocumentId = "doc-7"
        };

        await retriever.RetrieveAsync("查询", 3, filter, "openai", TestContext.Current.CancellationToken);

        Assert.Equal(3, fake.LastTopK);
        Assert.Same(filter, fake.LastFilter);
        Assert.Equal("openai", fake.LastProvider);
    }

    /// <summary>
    /// 查询文本原样抵达实现侧，不做修剪或改写
    /// </summary>
    /// <param name="query">用户查询</param>
    /// <remarks>查询会被直接送去做嵌入，任何预处理都属实现策略，抽象层不得代劳。</remarks>
    [Theory]
    [InlineData("曦寒框架是什么")]
    [InlineData("  前后带空白  ")]
    [InlineData("multi\nline query")]
    public async Task RetrieveAsync_WithQuery_PassesQueryVerbatim(string query)
    {
        var fake = new RecordingKnowledgeRetriever();
        IKnowledgeRetriever retriever = fake;

        await retriever.RetrieveAsync(query, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(query, fake.LastQuery);
    }

    /// <summary>
    /// 检索结果为只读列表，按相近度排序由实现保证
    /// </summary>
    [Fact]
    public async Task RetrieveAsync_ReturnsReadOnlyChunkList()
    {
        IKnowledgeRetriever retriever = new RecordingKnowledgeRetriever();

        var chunks = await retriever.RetrieveAsync("查询", 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("doc-0", chunks[0].DocumentId);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(1, chunks[1].Index);
    }

    /// <summary>
    /// 检索方法的完整签名与可选参数默认值锁定
    /// </summary>
    /// <remarks>
    /// 参数顺序同样是契约：现有调用点大量使用位置实参，调换 filter 与 provider 的位置
    /// 会让 <c>RetrieveAsync(q, 5, null, "openai")</c> 这类调用编译通过但语义颠倒。
    /// </remarks>
    [Fact]
    public void RetrieveAsync_Signature_HasStableParameterOrderAndDefaults()
    {
        var method = typeof(IKnowledgeRetriever).GetMethod(nameof(IKnowledgeRetriever.RetrieveAsync))!;

        Assert.Equal(typeof(Task<IReadOnlyList<RetrievedChunk>>), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(5, parameters.Length);

        Assert.Equal("query", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("topK", parameters[1].Name);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(5, (int)parameters[1].DefaultValue!);

        Assert.Equal("filter", parameters[2].Name);
        Assert.Equal(typeof(RetrievalFilter), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
        Assert.Null(parameters[2].DefaultValue);

        Assert.Equal("provider", parameters[3].Name);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        Assert.True(parameters[3].IsOptional);
        Assert.Null(parameters[3].DefaultValue);

        Assert.Equal("cancellationToken", parameters[4].Name);
        Assert.Equal(typeof(CancellationToken), parameters[4].ParameterType);
        Assert.True(parameters[4].IsOptional);
    }

    /// <summary>
    /// 取消令牌一路传到实现侧
    /// </summary>
    [Fact]
    public async Task RetrieveAsync_WhenTokenAlreadyCanceled_Throws()
    {
        IKnowledgeRetriever retriever = new RecordingKnowledgeRetriever();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await retriever.RetrieveAsync("查询", cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 记录入参并按 topK 生成占位结果的检索器替身
    /// </summary>
    private sealed class RecordingKnowledgeRetriever : IKnowledgeRetriever
    {
        /// <summary>
        /// 最近一次收到的查询文本
        /// </summary>
        public string? LastQuery { get; private set; }

        /// <summary>
        /// 最近一次收到的 topK
        /// </summary>
        public int? LastTopK { get; private set; }

        /// <summary>
        /// 最近一次收到的过滤条件
        /// </summary>
        public RetrievalFilter? LastFilter { get; private set; }

        /// <summary>
        /// 最近一次收到的 provider
        /// </summary>
        public string? LastProvider { get; private set; }

        /// <summary>
        /// 记录入参并回放 topK 条占位切片
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="topK">召回条数</param>
        /// <param name="filter">过滤条件</param>
        /// <param name="provider">嵌入 provider</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
            string query,
            int topK = 5,
            RetrievalFilter? filter = null,
            string? provider = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQuery = query;
            LastTopK = topK;
            LastFilter = filter;
            LastProvider = provider;

            var chunks = new List<RetrievedChunk>();

            for (var index = 0; index < topK; index++)
            {
                chunks.Add(new RetrievedChunk
                {
                    DocumentId = "doc-" + index.ToString(),
                    Index = index,
                    Text = "片段" + index.ToString(),
                    Score = 1d - (index * 0.1d)
                });
            }

            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(chunks);
        }
    }
}
