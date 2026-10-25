// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 聊天入口：提示词成系统消息、工具绑定、护栏、Provider 解析与缓存、异常映射、流式。
/// </summary>
public sealed class AIChatTests
{
    /// <summary>
    /// 基础配置：一个 fake Provider、一个提示词、开启关键词护栏。
    /// </summary>
    private static readonly (string Key, string Value)[] _configuration =
    [
        ("XiHan:AI:Providers:fake:Kind", "fake"),
        ("XiHan:AI:Providers:fake:ChatModel", "model-1"),
        ("XiHan:AI:Prompts:assistant:Template", "You are {name}."),
        ("XiHan:AI:Guardrails:Enabled", "true"),
        ("XiHan:AI:Guardrails:BlockedKeywords:0", "forbidden"),
    ];

    [Fact]
    public async Task Complete_RendersPrompt_BindsTools_AndCallsTheDefaultProvider()
    {
        var factory = new FakeChatClientFactory();
        await using var host = await StartAsync(factory);
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        var reply = (await chat.CompleteAsync(new AIChatRequest
        {
            Messages = [new ChatMessage(ChatRole.User, "hi")],
            Prompt = "assistant",
            PromptArguments = new Dictionary<string, object?> { ["name"] = "Echo" },
            Tools = ["echoes_create"],
            Options = new ChatOptions { Temperature = 0.1f },
        }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal("pong", reply.Text);

        var client = Assert.Single(factory.Created);
        Assert.Equal([ChatRole.System, ChatRole.User], client.LastMessages!.Select(static message => message.Role));
        Assert.Equal("You are Echo.", client.LastMessages![0].Text);
        Assert.Equal(0.1f, client.LastOptions!.Temperature);
        var tool = Assert.IsType<OperationAIFunction>(Assert.Single(client.LastOptions.Tools!));
        Assert.Equal("echoes_create", tool.Name);

        // 同一 Provider 复用客户端；失效后重建
        await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "again")] }, CancellationToken.None);
        Assert.Single(factory.Created);
        host.Services.GetRequiredService<IAIProviderCatalog>().Invalidate("fake");
        Assert.Equal(1, client.Disposed);
        await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "again")] }, CancellationToken.None);
        Assert.Equal(2, factory.Created.Count);
    }

    [Fact]
    public async Task Complete_ReportsGuardrailsAndLookupFailures()
    {
        var factory = new FakeChatClientFactory();
        await using var host = await StartAsync(factory);
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();

        var blocked = await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "this is forbidden")] }, CancellationToken.None);
        Assert.Equal(AIErrors.GuardrailBlocked, blocked.Error?.Code);
        Assert.Equal(KeywordBlocklistGuardrail.GuardrailName, blocked.Error!.Details!["guardrail"]);
        Assert.Empty(factory.Created);
        Assert.True((await chat.CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "this is forbidden")], SkipGuardrails = true }, CancellationToken.None)).IsSuccess);

        Assert.Equal(AIErrors.PromptNotFound, (await chat.CompleteAsync(new AIChatRequest { Messages = [], Prompt = "missing" }, CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.RequestInvalid, (await chat.CompleteAsync(new AIChatRequest { Messages = [], Prompt = "assistant" }, CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.ToolNotFound, (await chat.CompleteAsync(new AIChatRequest { Messages = [], Tools = ["nope"] }, CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.ProviderNotFound, (await chat.CompleteAsync(new AIChatRequest { Messages = [], Provider = "other" }, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Complete_MapsClientFailures_AndStreams()
    {
        var factory = new FakeChatClientFactory();
        await using var host = await StartAsync(factory);
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();
        var request = new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] };

        Assert.True((await chat.CompleteAsync(request, CancellationToken.None)).IsSuccess);
        var client = Assert.Single(factory.Created);
        client.Throw = new InvalidOperationException("endpoint down");
        var unavailable = await chat.CompleteAsync(request, CancellationToken.None);
        Assert.Equal(AIErrors.ModelUnavailable, unavailable.Error?.Code);
        Assert.Equal("endpoint down", unavailable.Error!.Details!["reason"]);
        Assert.Equal("fake", unavailable.Error.Details["provider"]);

        client.Throw = new OperationCanceledException();
        Assert.Equal(AIErrors.ModelTimeout, (await chat.CompleteAsync(request, CancellationToken.None)).Error?.Code);
        client.Throw = new TimeoutException();
        Assert.Equal(AIErrors.ModelTimeout, (await chat.CompleteAsync(request, CancellationToken.None)).Error?.Code);

        // 调用方自己取消：异常原样抛出
        client.Throw = null;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        client.Throw = new OperationCanceledException(cancelled.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await chat.CompleteAsync(request, cancelled.Token));

        client.Throw = null;
        var stream = (await chat.StreamAsync(request with { Prompt = "assistant", PromptArguments = new Dictionary<string, object?> { ["name"] = "S" } }, CancellationToken.None)).ValueOrThrow();
        var text = new System.Text.StringBuilder();
        await foreach (var update in stream)
        {
            text.Append(update.Text);
        }

        Assert.Equal("pong", text.ToString());
        Assert.Equal("You are S.", client.LastMessages![0].Text);
        Assert.Equal(AIErrors.GuardrailBlocked, (await chat.StreamAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "forbidden")] }, CancellationToken.None)).Error?.Code);
    }

    /// <summary>
    /// 启动：工具目录 + 回显操作，fake 聊天工厂。
    /// </summary>
    private static Task<TestHost> StartAsync(FakeChatClientFactory factory)
        => TestHost.StartAsync(
            static b => b.UseFeature<AIToolsFeature>().UseFeature<EchoFeature>(),
            _configuration.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            services => services.AddSingleton<IChatClientFactory>(factory));
}
