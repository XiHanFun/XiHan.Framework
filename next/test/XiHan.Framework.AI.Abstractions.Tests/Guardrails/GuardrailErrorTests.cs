// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Guardrails;

/// <summary>
/// 护栏出错（RFC-0022、ADR-0114）
/// </summary>
/// <remarks>
/// 护栏抛出时，错误里的 <c>reason</c> 只写异常类型，完整异常进日志；护栏自己返回的失败是它写给调用方的文案，原样保留；取消照常抛出
/// </remarks>
public sealed class GuardrailErrorTests
{
    /// <summary>
    /// 护栏的异常消息里带着不该外露的地址
    /// </summary>
    private const string Secret = "moderation.internal:9000";

    [Fact]
    public async Task Thrown_ReportsOnlyTheExceptionType_AndLogsTheFullException()
    {
        var (guardrail, logs, chat, host) = await StartAsync();
        await using var _ = host;
        var fault = new HttpRequestException($"Connection refused by {Secret}.");
        guardrail.Throw = fault;

        var failed = await chat.CompleteAsync(Request(), CancellationToken.None);

        Assert.NotNull(failed.Error);
        Assert.Equal(AIErrors.Unexpected, failed.Error.Code);
        Assert.Equal(AIMessages.GuardrailFailed, failed.Error.MessageKey);
        Assert.Equal(guardrail.Name, failed.Error.Details?["guardrail"]);
        Assert.Equal(typeof(HttpRequestException).FullName, failed.Error.Details?["reason"]);
        Assert.DoesNotContain(Secret, failed.Error.Message, StringComparison.Ordinal);

        var logged = Assert.Single(Entries(logs));
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Same(fault, logged.Exception);
        Assert.Contains($"'{guardrail.Name}'", logged.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returned_KeepsTheGuardrailsOwnMessage_AndLogsNothing()
    {
        var (guardrail, logs, chat, host) = await StartAsync();
        await using var _ = host;
        guardrail.Fail = Error.Literal("MODERATION_UNEXPECTED_001", "The moderation service rejected the request.");

        var failed = await chat.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal(AIErrors.Unexpected, failed.Error?.Code);
        Assert.Equal(guardrail.Fail.Message, failed.Error?.Details?["reason"]);
        Assert.Empty(Entries(logs));
    }

    [Fact]
    public async Task Cancellation_IsThrown_AndNotLogged()
    {
        var (guardrail, logs, chat, host) = await StartAsync();
        await using var _ = host;
        guardrail.Throw = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await chat.CompleteAsync(Request(), CancellationToken.None));

        Assert.Empty(Entries(logs));
    }

    /// <summary>
    /// 一条普通请求
    /// </summary>
    private static AIChatRequest Request() => new() { Messages = [new ChatMessage(ChatRole.User, "hi")], Provider = "fake" };

    /// <summary>
    /// 聊天组件这个类别下带异常的日志
    /// </summary>
    private static List<(LogLevel Level, string Category, string Message, Exception? Exception)> Entries(LogCapture logs)
        => logs.Entries.Where(static entry => entry.Category == typeof(AIChat).FullName && entry.Exception is not null).ToList();

    /// <summary>
    /// 启动装配了脚本护栏的宿主
    /// </summary>
    private static async Task<(ScriptedGuardrail Guardrail, LogCapture Logs, IAIChat Chat, IAsyncDisposable Host)> StartAsync()
    {
        var guardrail = new ScriptedGuardrail();
        var logs = new LogCapture();
        var host = await TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>(),
            [new("XiHan:AI:Providers:fake:Kind", "fake"), new("XiHan:AI:Providers:fake:ChatModel", "model-1")],
            services => services.AddSingleton<IChatClientFactory>(new FakeChatClientFactory()).AddSingleton<IAIGuardrail>(guardrail).AddSingleton<ILoggerFactory>(logs));
        var scope = host.Services.CreateAsyncScope();
        return (guardrail, logs, scope.ServiceProvider.GetRequiredService<IAIChat>(), new HostAndScope(host, scope));
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
