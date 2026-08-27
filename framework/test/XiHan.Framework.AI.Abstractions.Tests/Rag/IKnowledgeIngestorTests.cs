// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 知识摄取器契约测试
/// </summary>
/// <remarks>
/// 摄取返回切片数、移除按切片数删除——这个「数字要自己记账」的设计是本接口最关键的约定：
/// 向量库里没有按文档删除的原生能力，切片数是重建/删除时唯一的定位依据，
/// 因此摄取的返回值必须被调用方持久化，两个方法的口径必须一致。
/// </remarks>
public class IKnowledgeIngestorTests
{
    /// <summary>
    /// 摄取返回本次写入的切片数
    /// </summary>
    [Fact]
    public async Task IngestAsync_ReturnsChunkCount()
    {
        var ingestor = new RecordingKnowledgeIngestor(ingestedChunkCount: 7);

        var count = await ingestor.IngestAsync(
            new KnowledgeIngestRequest { DocumentId = "doc-1", Text = "正文" },
            TestContext.Current.CancellationToken);

        Assert.Equal(7, count);
    }

    /// <summary>
    /// 摄取请求原样抵达实现侧
    /// </summary>
    [Fact]
    public async Task IngestAsync_PassesSameRequestInstance()
    {
        var ingestor = new RecordingKnowledgeIngestor(ingestedChunkCount: 1);
        var request = new KnowledgeIngestRequest
        {
            DocumentId = "doc-1",
            Text = "正文",
            TenantId = 1024,
            Provider = "openai",
            Chunking = new ChunkingOptions { MaxChunkSize = 400, Overlap = 40 }
        };

        await ingestor.IngestAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(request, ingestor.LastRequest);
        Assert.Equal(1024L, ingestor.LastRequest!.TenantId);
    }

    /// <summary>
    /// 移除文档时按摄取返回的切片数逐片定位
    /// </summary>
    /// <param name="chunkCount">该文档原切片数</param>
    /// <remarks>
    /// 切片数为 0 是合法入参（空文档或从未摄取成功），实现应当安全空转，
    /// 因此这里把 0 与正常值一起覆盖。
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(128)]
    public async Task RemoveDocumentAsync_PassesDocumentIdAndChunkCount(int chunkCount)
    {
        var ingestor = new RecordingKnowledgeIngestor(ingestedChunkCount: 0);

        await ingestor.RemoveDocumentAsync("doc-42", chunkCount, TestContext.Current.CancellationToken);

        Assert.Equal("doc-42", ingestor.LastRemovedDocumentId);
        Assert.Equal(chunkCount, ingestor.LastRemovedChunkCount);
    }

    /// <summary>
    /// 摄取的签名：请求必填、取消令牌可选，返回切片数
    /// </summary>
    [Fact]
    public void IngestAsync_Signature_ReturnsChunkCountTask()
    {
        var method = typeof(IKnowledgeIngestor).GetMethod(nameof(IKnowledgeIngestor.IngestAsync))!;

        Assert.Equal(typeof(Task<int>), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(KnowledgeIngestRequest), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
    }

    /// <summary>
    /// 移除的签名：文档 id 与切片数均必填，没有默认值可省
    /// </summary>
    /// <remarks>
    /// 切片数若给了默认值（比如 0），调用方漏传时会静默删不掉任何向量，
    /// 表面成功、实际残留，故必须强制显式传入。
    /// </remarks>
    [Fact]
    public void RemoveDocumentAsync_Signature_RequiresBothDocumentIdAndChunkCount()
    {
        var method = typeof(IKnowledgeIngestor).GetMethod(nameof(IKnowledgeIngestor.RemoveDocumentAsync))!;

        Assert.Equal(typeof(Task), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal("documentId", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("chunkCount", parameters[1].Name);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
    }

    /// <summary>
    /// 取消令牌一路传到实现侧
    /// </summary>
    [Fact]
    public async Task IngestAsync_WhenTokenAlreadyCanceled_Throws()
    {
        var ingestor = new RecordingKnowledgeIngestor(ingestedChunkCount: 1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "doc-1", Text = "正文" }, cts.Token);
        });
    }

    /// <summary>
    /// 记录入参并回放固定切片数的摄取器替身
    /// </summary>
    private sealed class RecordingKnowledgeIngestor : IKnowledgeIngestor
    {
        private readonly int _ingestedChunkCount;

        /// <summary>
        /// 构造摄取器替身
        /// </summary>
        /// <param name="ingestedChunkCount">摄取时回放的切片数</param>
        public RecordingKnowledgeIngestor(int ingestedChunkCount)
        {
            _ingestedChunkCount = ingestedChunkCount;
        }

        /// <summary>
        /// 最近一次收到的摄取请求
        /// </summary>
        public KnowledgeIngestRequest? LastRequest { get; private set; }

        /// <summary>
        /// 最近一次被移除的文档 id
        /// </summary>
        public string? LastRemovedDocumentId { get; private set; }

        /// <summary>
        /// 最近一次移除时给出的切片数
        /// </summary>
        public int? LastRemovedChunkCount { get; private set; }

        /// <summary>
        /// 记录摄取请求并回放切片数
        /// </summary>
        /// <param name="request">摄取请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int> IngestAsync(KnowledgeIngestRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;

            return Task.FromResult(_ingestedChunkCount);
        }

        /// <summary>
        /// 记录移除请求
        /// </summary>
        /// <param name="documentId">文档 id</param>
        /// <param name="chunkCount">该文档原切片数</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRemovedDocumentId = documentId;
            LastRemovedChunkCount = chunkCount;

            return Task.CompletedTask;
        }
    }
}
