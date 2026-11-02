// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Providers;

/// <summary>
/// Provider 的超时令牌与调用方取消的归因。
/// </summary>
/// <remarks>
/// 替身 Provider 带自己的传输超时（与 OpenAI SDK 同形），在闸门处停住；用例按需推进注入的时钟（触发超时令牌）与取消调用方令牌之后才放行，
/// Provider 再抛出取消或接住取消交回截断的结果：观察时该触发的令牌都已触发，结果与触发顺序、线程调度无关。
/// 超时令牌已触发即超时，调用方同时取消也一样，抛出与写成结果同一规则（RFC-0022「超时与取消」，ADR-0095）。
/// </remarks>
public sealed class ProviderTimeoutAttributionTests
{
    /// <summary>
    /// Provider 的时限。
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 基础配置：一个 gated Provider，30 秒时限，知识库 4 维。
    /// </summary>
    private static readonly (string Key, string Value)[] _configuration =
    [
        ("XiHan:AI:Providers:gated:Kind", GatedChatClientFactory.KindName),
        ("XiHan:AI:Providers:gated:ChatModel", "gated-chat"),
        ("XiHan:AI:Providers:gated:TimeoutSeconds", "30"),
        ("XiHan:AI:Knowledge:Dimensions", "4"),
    ];

    /// <summary>
    /// Provider 观察令牌之前触发了哪些令牌。
    /// </summary>
    public enum Trigger
    {
        /// <summary>
        /// 只有 Provider 的时限到了。
        /// </summary>
        ProviderOnly,

        /// <summary>
        /// 只有调用方取消。
        /// </summary>
        CallerOnly,

        /// <summary>
        /// 时限先到，调用方随后也取消。
        /// </summary>
        ProviderThenCaller,

        /// <summary>
        /// 调用方先取消，时限随后也到。
        /// </summary>
        CallerThenProvider,
    }

    /// <summary>
    /// 经哪条路径调用 Provider。
    /// </summary>
    public enum Route
    {
        /// <summary>
        /// 完整应答。
        /// </summary>
        Complete,

        /// <summary>
        /// 流式应答的第二个更新。
        /// </summary>
        Stream,

        /// <summary>
        /// 摄入的嵌入。
        /// </summary>
        Ingest,

        /// <summary>
        /// 检索的嵌入。
        /// </summary>
        Retrieve,
    }

    /// <summary>
    /// 全部组合：路径 × 触发 × Provider 抛出或写成结果。
    /// </summary>
    public static TheoryData<Route, Trigger, bool> Cases()
    {
        var data = new TheoryData<Route, Trigger, bool>();
        foreach (var route in Enum.GetValues<Route>())
        {
            foreach (var trigger in Enum.GetValues<Trigger>())
            {
                data.Add(route, trigger, true);
                data.Add(route, trigger, false);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ProviderTimeout_IsAttributedByItsToken_WhetherTheProviderThrowsOrReturns(Route route, Trigger trigger, bool throws)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
        var gate = new ProviderGate { Throws = throws };
        await using var host = await StartAsync(time, gate);
        await using var scope = host.Services.CreateAsyncScope();
        using var caller = new CancellationTokenSource();

        var invocation = route switch
        {
            Route.Complete => CompleteAsync(scope.ServiceProvider, caller.Token),
            Route.Stream => StreamAsync(scope.ServiceProvider, caller.Token),
            Route.Ingest => IngestAsync(scope.ServiceProvider, caller.Token),
            _ => RetrieveAsync(scope.ServiceProvider, caller.Token),
        };
        await gate.Waiting;

        // 推进时钟到时限，框架的超时令牌与 Provider 自己的超时在 Advance 里同步触发；调用方的令牌按场景先后取消
        if (trigger is Trigger.CallerOnly or Trigger.CallerThenProvider)
        {
            await caller.CancelAsync();
        }

        if (trigger is not Trigger.CallerOnly)
        {
            time.Advance(_timeout);
        }

        if (trigger is Trigger.ProviderThenCaller)
        {
            await caller.CancelAsync();
        }

        gate.Release();
        var outcome = await invocation;

        // 流已交给调用方，超时以 TimeoutException 抛出；其余路径以结果返回
        var timedOut = route == Route.Stream ? nameof(TimeoutException) : AIErrors.ModelTimeout;
        Assert.Equal(trigger == Trigger.CallerOnly ? nameof(OperationCanceledException) : timedOut, outcome);
    }

    [Fact]
    public async Task Stream_TimesOnlyTheWaitForEachUpdate()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
        var gate = new ProviderGate { Throws = true };
        await using var host = await StartAsync(time, gate);
        await using var scope = host.Services.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAIChat>();
        var stream = (await chat.StreamAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, CancellationToken.None)).ValueOrThrow();
        await using var updates = stream.GetAsyncEnumerator();

        // 拿到第一个更新之后，调用方处理它用了两倍时限：不在等 Provider，不计时
        Assert.True(await updates.MoveNextAsync());
        time.Advance(_timeout * 2);

        // 等第二个更新时重新计时：差一个 tick 到时限时放行，照常拿到
        var next = updates.MoveNextAsync().AsTask();
        await gate.Waiting;
        time.Advance(_timeout - TimeSpan.FromTicks(1));
        gate.Release();
        Assert.True(await next);
        Assert.Equal("second", updates.Current.Text);
        Assert.False(await updates.MoveNextAsync());
    }

    /// <summary>
    /// 启动：gated 聊天与嵌入工厂、注入的时钟、进程内知识库存储。
    /// </summary>
    private static Task<TestHost> StartAsync(FakeTimeProvider time, ProviderGate gate)
        => TestHost.StartAsync(
            static b => b.UseFeature<AIToolsFeature>(),
            _configuration.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            services =>
            {
                services.AddSingleton<TimeProvider>(time);
                services.AddSingleton<IChatClientFactory>(new GatedChatClientFactory(time, gate));
                services.AddSingleton<IEmbeddingGeneratorFactory>(new GatedEmbeddingGeneratorFactory(time, gate));
                services.AddSingleton<IKnowledgeStore>(new FakeKnowledgeStore());
            });

    /// <summary>
    /// 完整应答：成功、错误码或抛出的异常。
    /// </summary>
    private static Task<string> CompleteAsync(IServiceProvider services, CancellationToken cancellationToken)
        => ObserveAsync(async () =>
        {
            var reply = await services.GetRequiredService<IAIChat>().CompleteAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, cancellationToken);
            return reply.TryGetValue(out var response) ? $"success '{response.Text}'" : reply.Error!.Code;
        });

    /// <summary>
    /// 流式应答：取完第一个更新，再看第二个。
    /// </summary>
    private static Task<string> StreamAsync(IServiceProvider services, CancellationToken cancellationToken)
        => ObserveAsync(async () =>
        {
            var stream = (await services.GetRequiredService<IAIChat>().StreamAsync(new AIChatRequest { Messages = [new ChatMessage(ChatRole.User, "hi")] }, cancellationToken)).ValueOrThrow();
            var texts = new List<string>();
            await foreach (var update in stream.WithCancellation(cancellationToken))
            {
                texts.Add(update.Text);
            }

            return $"success '{string.Join('|', texts)}'";
        });

    /// <summary>
    /// 摄入一篇文档。
    /// </summary>
    private static Task<string> IngestAsync(IServiceProvider services, CancellationToken cancellationToken)
        => ObserveAsync(async () =>
        {
            var ingested = await services.GetRequiredService<IKnowledgeIngestor>().IngestAsync(new KnowledgeIngestRequest { DocumentId = "doc", Text = "some text" }, cancellationToken);
            return ingested.TryGetValue(out var chunks) ? $"success {chunks}" : ingested.Error!.Code;
        });

    /// <summary>
    /// 检索一次。
    /// </summary>
    private static Task<string> RetrieveAsync(IServiceProvider services, CancellationToken cancellationToken)
        => ObserveAsync(async () =>
        {
            var found = await services.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "some text" }, cancellationToken);
            return found.TryGetValue(out var chunks) ? $"success {chunks.Count}" : found.Error!.Code;
        });

    /// <summary>
    /// 把调用的结局写成字符串。
    /// </summary>
    /// <remarks>
    /// 结果原样，取消与超时异常按类型名，其他异常带类型名与消息。
    /// </remarks>
    private static async Task<string> ObserveAsync(Func<Task<string>> call)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException)
        {
            return nameof(OperationCanceledException);
        }
        catch (TimeoutException)
        {
            return nameof(TimeoutException);
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }
}
