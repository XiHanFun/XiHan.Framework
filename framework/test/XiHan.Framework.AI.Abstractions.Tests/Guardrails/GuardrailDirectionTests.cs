// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Guardrails;

/// <summary>
/// 护栏的检查方向（ADR-0166）
/// </summary>
/// <remarks>
/// 输入方向在调用模型之前，输出方向只在非流式调用拿到响应之后；跳过护栏的请求两个方向都不检查
/// </remarks>
public sealed class GuardrailDirectionTests
{
    [Fact]
    public async Task Complete_InspectsInputThenOutput()
    {
        var (guardrail, chat, host) = await StartAsync();
        await using var _ = host;

        var completed = await chat.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(completed.IsSuccess);
        Assert.Equal([GuardrailDirection.Input, GuardrailDirection.Output], guardrail.Inspections.Select(static inspection => inspection.Direction));
        Assert.Equal("hi", Assert.Single(guardrail.Inspections[0].Messages).Text);
        Assert.Equal("pong", Assert.Single(guardrail.Inspections[1].Messages).Text);
    }

    [Fact]
    public async Task Complete_BlockedOutput_IsNotReturned()
    {
        var (guardrail, chat, host) = await StartAsync();
        await using var _ = host;
        guardrail.BlockDirection = GuardrailDirection.Output;

        var blocked = await chat.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal(AIErrors.GuardrailBlocked, blocked.Error?.Code);
        Assert.Equal(guardrail.Name, blocked.Error?.Details?["guardrail"]);
    }

    [Fact]
    public async Task Stream_InspectsInputOnly()
    {
        var (guardrail, chat, host) = await StartAsync();
        await using var _ = host;
        guardrail.BlockDirection = GuardrailDirection.Output;

        var stream = (await chat.StreamAsync(Request(), CancellationToken.None)).ValueOrThrow();
        var text = new System.Text.StringBuilder();
        await foreach (var update in stream)
        {
            text.Append(update.Text);
        }

        Assert.Equal("pong", text.ToString());
        Assert.Equal(GuardrailDirection.Input, Assert.Single(guardrail.Inspections).Direction);
    }

    [Fact]
    public async Task SkipGuardrails_SkipsBothDirections()
    {
        var (guardrail, chat, host) = await StartAsync();
        await using var _ = host;
        guardrail.BlockDirection = GuardrailDirection.Output;

        var completed = await chat.CompleteAsync(Request() with { SkipGuardrails = true }, CancellationToken.None);

        Assert.True(completed.IsSuccess);
        Assert.Empty(guardrail.Inspections);
    }

    /// <summary>
    /// 一条普通请求
    /// </summary>
    private static AIChatRequest Request() => new() { Messages = [new ChatMessage(ChatRole.User, "hi")], Provider = "fake" };

    /// <summary>
    /// 启动装配了脚本护栏的宿主
    /// </summary>
    private static async Task<(ScriptedGuardrail Guardrail, IAIChat Chat, IAsyncDisposable Host)> StartAsync()
    {
        var guardrail = new ScriptedGuardrail();
        var host = await TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>(),
            [new("XiHan:AI:Providers:fake:Kind", "fake"), new("XiHan:AI:Providers:fake:ChatModel", "model-1")],
            services => services.AddSingleton<IChatClientFactory>(new FakeChatClientFactory()).AddSingleton<IAIGuardrail>(guardrail));
        var scope = host.Services.CreateAsyncScope();
        return (guardrail, scope.ServiceProvider.GetRequiredService<IAIChat>(), new HostAndScope(host, scope));
    }

    /// <summary>
    /// 一起释放宿主与作用域
    /// </summary>
    private sealed class HostAndScope(TestHost host, AsyncServiceScope scope) : IAsyncDisposable
    {
        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await host.DisposeAsync();
        }
    }
}
