// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.OpenAI.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// OpenAI Provider：请求形状、工具调用经管道执行、流式、嵌入维度、配置缺失。
/// </summary>
public sealed class OpenAIFeatureTests
{
    [Fact]
    public async Task Chat_SendsModelAndMessages_AndRunsToolCallsThroughThePipeline()
    {
        var handler = new FakeOpenAIHandler { ToolToCall = ("echoes_create", """{"text":"hi","repeat":2}""") };
        await using var host = await OpenAITestHost.StartAsync(handler);
        Assert.Equal((ManifestKind.Provider, ManifestRole.Provider), (new OpenAIFeature().Manifest.Kind, new OpenAIFeature().Manifest.Role));
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        var reply = (await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "echo please")], Tools = ["echoes_create"] }, CancellationToken.None)).ValueOrThrow();
        Assert.Contains("hihi", reply.Text, StringComparison.Ordinal);
        Assert.StartsWith("Tool said:", reply.Text, StringComparison.Ordinal);

        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0].Body;
        Assert.EndsWith("/v1/chat/completions", handler.Requests[0].Path, StringComparison.Ordinal);
        Assert.Equal("chat-model", first["model"]!.GetValue<string>());
        Assert.Equal("echo please", first["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("echoes_create", first["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal("object", first["tools"]![0]!["function"]!["parameters"]!["type"]!.GetValue<string>());
        var second = handler.Requests[1].Body["messages"]!.AsArray();
        Assert.Equal("tool", second[^1]!["role"]!.GetValue<string>());
        Assert.Contains("hihi", second[^1]!["content"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_Streams_AndEmbeddingsUseConfiguredDimensions()
    {
        var handler = new FakeOpenAIHandler { Reply = "streamed" };
        await using var host = await OpenAITestHost.StartAsync(handler);
        await using var scope = host.Services.CreateAsyncScope();
        var stream = (await scope.ServiceProvider.GetRequiredService<IAIChat>().StreamAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "go")] }, CancellationToken.None)).ValueOrThrow();
        var text = new StringBuilder();
        await foreach (var update in stream)
        {
            text.Append(update.Text);
        }

        Assert.Equal("streamed", text.ToString());
        Assert.True(handler.Requests[^1].Body["stream"]!.GetValue<bool>());

        var generator = host.Services.GetRequiredService<IAIProviderCatalog>().ResolveEmbeddingGenerator().ValueOrThrow();
        var embeddings = await generator.GenerateAsync(["alpha", "beta"], cancellationToken: CancellationToken.None);
        Assert.Equal(2, embeddings.Count);
        Assert.All(embeddings, embedding => Assert.Equal(OpenAITestHost.Dimensions, embedding.Vector.Length));
        var request = handler.Requests[^1].Body;
        Assert.EndsWith("/v1/embeddings", handler.Requests[^1].Path, StringComparison.Ordinal);
        Assert.Equal("embedding-model", request["model"]!.GetValue<string>());
        Assert.Equal(OpenAITestHost.Dimensions, request["dimensions"]!.GetValue<int>());
    }

    [Fact]
    public async Task MissingModels_AreValidationErrors()
    {
        await using var host = await OpenAITestHost.StartAsync(new FakeOpenAIHandler(), ("XiHan:AI:Providers:bare:Kind", "openai"), ("XiHan:AI:Providers:bare:Endpoint", "http://localhost/v1"));
        var catalog = host.Services.GetRequiredService<IAIProviderCatalog>();
        Assert.Equal(AIErrors.RequestInvalid, catalog.ResolveChatClient("bare").Error?.Code);
        Assert.Equal(AIErrors.RequestInvalid, catalog.ResolveEmbeddingGenerator("bare").Error?.Code);
        Assert.True(catalog.ResolveChatClient(OpenAITestHost.ProviderName).IsSuccess);
    }

    [Fact]
    public async Task OnlyTheOpenAIFeature_ValidatesTheKnowledgePromptAtStartup()
    {
        // 只装 OpenAI Provider（不装工具目录与知识库 Provider）：知识库配置引用的提示词同样在启动时校验，而不是第一次解析增强器时
        var missing = await Assert.ThrowsAsync<FrameworkException>(() => StartOnlyOpenAIAsync(("XiHan:AI:Knowledge:PromptName", "rag")));
        Assert.Contains(AIErrors.PromptNotFound, CodesOf(missing));
        var unknownPlaceholder = await Assert.ThrowsAsync<FrameworkException>(() => StartOnlyOpenAIAsync(("XiHan:AI:Knowledge:PromptName", "rag"), ("XiHan:AI:Prompts:rag:Template", "Answer in {language}: {question} {context}")));
        Assert.Contains(AIErrors.RequestInvalid, CodesOf(unknownPlaceholder));

        // 配置正确时照常启动，增强只用配置的模板
        await using var host = await StartOnlyOpenAIAsync(("XiHan:AI:Knowledge:PromptName", "rag"), ("XiHan:AI:Prompts:rag:Template", "CTX:{context}|Q:{question}"));
        Assert.StartsWith("CTX:", host.Services.GetRequiredService<IRagPromptAugmenter>().Augment("why?", [new RetrievedChunk { Id = "d1#0", DocumentId = "d1", ChunkIndex = 0, Text = "because", Score = 1 }]), StringComparison.Ordinal);
    }

    /// <summary>
    /// 只装 OpenAI Provider 启动，请求不触网。
    /// </summary>
    private static Task<TestHost> StartOnlyOpenAIAsync(params (string Key, string Value)[] configuration)
        => TestHost.StartAsync(static b => b.UseFeature<OpenAIFeature>(), configuration.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));

    /// <summary>
    /// 异常链上全部框架错误码（启动失败由内核包一层）。
    /// </summary>
    private static List<string> CodesOf(Exception exception)
    {
        var codes = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is FrameworkException framework)
            {
                codes.Add(framework.Code);
            }
        }

        return codes;
    }
}
