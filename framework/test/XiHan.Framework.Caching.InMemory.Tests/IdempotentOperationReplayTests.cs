// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory.Tests.Fixtures;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 幂等回放操作的响应（RFC-0006 幂等第 4 条，ADR-0064）
/// </summary>
/// <remarks>
/// XGI 入口拿到首次调用的响应载荷，类型化入口拿到首次调用的响应对象，处理器只执行一次；同一个键被另一个操作或非操作调用用过、记录里没有响应时明确失败，不返回空载荷
/// </remarks>
public sealed class IdempotentOperationReplayTests
{
    [Fact]
    public async Task XgiReplay_ReturnsTheFirstResponse_WithoutExecutingAgain()
    {
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();

        var first = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("k1"));
        // 同一个键、不同的请求体：回放首次调用的响应，不执行处理器
        var second = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "bob"), Key("k1"));

        Assert.True(first.IsSuccess, first.Outcome.ToString());
        Assert.True(second.IsSuccess, second.Outcome.ToString());
        Assert.Equal(first.Payload.ToArray(), second.Payload.ToArray());
        Assert.Equal(new TicketReply(1, "alice"), Decode(second.Payload));
        Assert.Equal(1, counter.Executions);

        // 换一个键照常执行
        Assert.Equal(new TicketReply(2, "bob"), Decode((await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "bob"), Key("k1-other"))).Payload));
    }

    [Fact]
    public async Task TypedReplay_ReturnsTheFirstResponse_WhicheverEntryMadeTheFirstCall()
    {
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();

        // 首次走 XGI、回放走类型化：响应按描述解码回 TicketReply，而不是 KERNEL_UNEXPECTED
        Assert.True((await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("k2"))).IsSuccess);
        var replayed = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("bob"), Key("k2"));
        Assert.Equal(new TicketReply(1, "alice"), replayed.ValueOrThrow());

        // 两次都走类型化
        var first = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("carol"), Key("k3"));
        var second = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("dave"), Key("k3"));
        Assert.Equal(new TicketReply(2, "carol"), first.ValueOrThrow());
        Assert.Equal(first.ValueOrThrow(), second.ValueOrThrow());
        Assert.Equal(2, counter.Executions);
    }

    [Fact]
    public async Task FailedOperation_ReplaysTheFailure_ThroughBothEntries()
    {
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();

        var typed = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("fail"), Key("k4"));
        var typedAgain = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("fail"), Key("k4"));
        var raw = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "fail"), Key("k4"));

        Assert.Equal("TICKETS_CONFLICT_001", typed.Error?.Code);
        Assert.Equal("TICKETS_CONFLICT_001", typedAgain.Error?.Code);
        Assert.Equal("TICKETS_CONFLICT_001", raw.Outcome.Error?.Code);
        Assert.True(raw.Payload.IsEmpty);
        Assert.Equal(1, counter.Executions);
    }

    [Fact]
    public async Task KeyReused_ByAnotherOperation_OrAPlainInvocation_IsAConflict()
    {
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();
        var plainExecutions = 0;

        Assert.True((await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("k5"))).IsSuccess);

        // 另一个操作拿同一个键：不能把 tickets.create 的响应当成 tickets.update 的响应
        var otherOperation = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Update, new TicketRequest("alice"), Key("k5"));
        Assert.Equal(CachingErrors.IdempotentMismatch, otherOperation.Error?.Code);
        Assert.Equal(CachingMessages.IdempotentKeyReused, otherOperation.Error?.MessageKey);
        Assert.Equal(TicketsFeature.Update, otherOperation.Error?.Details?["operation"]);
        Assert.Equal(TicketsFeature.Create, otherOperation.Error?.Details?["recorded"]);

        // 非操作调用拿操作用过的键，与反过来，都是冲突
        var plain = await host.InvokeAsync(_ => { plainExecutions++; return ValueTask.CompletedTask; }, baggage: Key("k5"));
        Assert.Equal(CachingErrors.IdempotentMismatch, plain.Error?.Code);
        Assert.Equal("-", plain.Error?.Details?["operation"]);
        Assert.True((await host.InvokeAsync(_ => { plainExecutions++; return ValueTask.CompletedTask; }, baggage: Key("k6"))).IsSuccess);
        var operationAfterPlain = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "bob"), Key("k6"));
        Assert.Equal(CachingErrors.IdempotentMismatch, operationAfterPlain.Outcome.Error?.Code);
        Assert.Equal("-", operationAfterPlain.Outcome.Error?.Details?["recorded"]);
        Assert.True(operationAfterPlain.Payload.IsEmpty);

        Assert.Equal(1, counter.Executions);
        Assert.Equal(1, plainExecutions);
    }

    [Fact]
    public async Task RecordsThatCannotBeReplayed_FailExplicitly_InsteadOfReturningAnEmptyPayload()
    {
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();
        var store = host.Services.GetRequiredService<ISharedCache>().Store;
        // 修复前写下的成功记录不带操作与响应；带了操作却没有响应的记录同样不能回放
        await store.SetAsync(new CacheKey("idem:-:-:legacy"), Encoding.UTF8.GetBytes("""{"state":"completed"}"""), CacheEntryOptions.Default, CancellationToken.None);
        await store.SetAsync(new CacheKey("idem:-:-:no-response"), Encoding.UTF8.GetBytes("""{"state":"completed","operation":"tickets.create"}"""), CacheEntryOptions.Default, CancellationToken.None);

        var legacy = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("legacy"));
        var raw = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("no-response"));
        var typed = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest("alice"), Key("no-response"));

        Assert.Equal(CachingErrors.IdempotentMismatch, legacy.Outcome.Error?.Code);
        Assert.Equal(CachingMessages.IdempotentKeyReused, legacy.Outcome.Error?.MessageKey);
        Assert.Equal(CachingErrors.IdempotentMismatch, raw.Outcome.Error?.Code);
        Assert.Equal(CachingMessages.IdempotentResponseUnavailable, raw.Outcome.Error?.MessageKey);
        Assert.True(raw.Payload.IsEmpty);
        Assert.Equal(CachingErrors.IdempotentMismatch, typed.Error?.Code);
        Assert.Equal(CachingMessages.IdempotentResponseUnavailable, typed.Error?.MessageKey);
        Assert.Equal(0, counter.Executions);
    }

    [Fact]
    public async Task AnEmptyResponse_IsRecordedAndReplayed_AsTheSameEmptyPayload()
    {
        // 插件代理交回的空载荷表示没有响应体（plugin/invocation.md §1、RFC-0006 幂等第 2 与第 4 条，ADR-0318）：记下没有响应体，回放出同样的空载荷；
        // 修之前记录时把空载荷当 JSON 解析而抛出，操作已经执行，首次调用却以 KERNEL_UNEXPECTED_001 结束，「处理中」的标记留到过期，重试得 CACHING_CONFLICT_002
        await using var host = await StartAsync();
        var operation = new EmptyResponseOperation("blanks.create");
        host.Services.GetRequiredService<OperationRegistry>().AddRange("xihan.test.blanks", [operation], []).ThrowIfFailure();

        var first = await host.InvokeOperationRawAsync(new OperationRequest(operation.Id, "{}"u8.ToArray()), Key("empty"));
        var second = await host.InvokeOperationRawAsync(new OperationRequest(operation.Id, "{}"u8.ToArray()), Key("empty"));

        Assert.True(first.IsSuccess, first.Outcome.ToString());
        Assert.True(first.Payload.IsEmpty);
        Assert.True(second.IsSuccess, second.Outcome.ToString());
        Assert.True(second.Payload.IsEmpty);
        Assert.Equal(1, operation.Executions);

        // 记录里写的是没有响应体，不是一个 JSON 值
        var record = (await host.Services.GetRequiredService<ISharedCache>().Store.GetAsync(new CacheKey("idem:-:-:empty"), CancellationToken.None)).ValueOrThrow();
        using var document = JsonDocument.Parse(record);
        Assert.True(document.RootElement.GetProperty("emptyResponse").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("response").ValueKind);

        // 有响应体的记录不写这个成员，与以前写下的记录一样
        Assert.True((await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("typed"))).IsSuccess);
        var typed = (await host.Services.GetRequiredService<ISharedCache>().Store.GetAsync(new CacheKey("idem:-:-:typed"), CancellationToken.None)).ValueOrThrow();
        using var typedDocument = JsonDocument.Parse(typed);
        Assert.False(typedDocument.RootElement.TryGetProperty("emptyResponse", out _));
        Assert.Equal(JsonValueKind.Object, typedDocument.RootElement.GetProperty("response").ValueKind);
    }

    [Fact]
    public async Task ARecordWithoutAResponseBody_IsDecodedAsAnEmptyPayload_AndTypedOperationsCannotReplayIt()
    {
        // 记下没有响应体的记录按描述解码空载荷：类型化的操作解码失败，与记录里没有响应一样返回 CACHING_CONFLICT_003，不返回空载荷
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();
        var store = host.Services.GetRequiredService<ISharedCache>().Store;
        await store.SetAsync(new CacheKey("idem:-:-:empty-body"), Encoding.UTF8.GetBytes("""{"state":"completed","operation":"tickets.create","emptyResponse":true}"""), CacheEntryOptions.Default, CancellationToken.None);

        var raw = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, "alice"), Key("empty-body"));

        Assert.Equal(CachingErrors.IdempotentMismatch, raw.Outcome.Error?.Code);
        Assert.Equal(CachingMessages.IdempotentResponseUnavailable, raw.Outcome.Error?.MessageKey);
        Assert.True(raw.Payload.IsEmpty);
        Assert.Equal(0, counter.Executions);
    }

    [Fact]
    public async Task AResponseThatCannotBeRecorded_ReplacesTheMarkerWithTheFailure_ThroughTheXgiEntry()
    {
        // 处理器成功、响应写不出（孤立的代理项）：用写不出的错误替换「处理中」标记并返回，重试回放同一个错误、不再执行（RFC-0006 幂等第 2 条，ADR-0324）；
        // 修之前记录时抛出的异常在清除标记的 try 之外：首次调用以 KERNEL_UNEXPECTED_001 结束，「处理中」的标记留到过期，这段时间里的重试得 CACHING_CONFLICT_002
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();

        var first = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, TicketsOperation.Unencodable), Key("unencodable-raw"));
        var second = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, TicketsOperation.Unencodable), Key("unencodable-raw"));

        AssertUnencodable(first.Outcome.Error);
        Assert.True(first.Payload.IsEmpty);
        AssertUnencodable(second.Outcome.Error);
        Assert.True(second.Payload.IsEmpty);
        Assert.Equal(1, counter.Executions);
        await AssertRecordedFailureAsync(host, "unencodable-raw");
    }

    [Fact]
    public async Task AResponseThatCannotBeRecorded_ReplacesTheMarkerWithTheFailure_ThroughTheTypedEntry()
    {
        // 类型化入口本身不编码响应，但幂等要把它记下：记不下与 XGI 入口相同，首次调用拿到同一个错误，重试从哪个入口来都回放它
        await using var host = await StartAsync();
        var counter = host.Services.GetRequiredService<TicketCounter>();

        var first = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest(TicketsOperation.Unencodable), Key("unencodable-typed"));
        var second = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest(TicketsOperation.Unencodable), Key("unencodable-typed"));
        var raw = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, TicketsOperation.Unencodable), Key("unencodable-typed"));

        AssertUnencodable(first.Error);
        AssertUnencodable(second.Error);
        AssertUnencodable(raw.Outcome.Error);
        Assert.True(raw.Payload.IsEmpty);
        Assert.Equal(1, counter.Executions);
        await AssertRecordedFailureAsync(host, "unencodable-typed");

        // 另一个操作拿同一个键：记录属于 tickets.create，照旧是键被复用，不回放这个错误
        var other = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Update, new TicketRequest("alice"), Key("unencodable-typed"));
        Assert.Equal(CachingErrors.IdempotentMismatch, other.Error?.Code);
        Assert.Equal(1, counter.Executions);
    }

    [Fact]
    public async Task AResponseThatCannotBeRecorded_IsLoggedOnce_WithTheOperationAndCode()
    {
        // 写不出的异常在这里接住、不抛出管道，Observe 阶段不记日志：修之前只有 Span 与指标里看得到失败（ADR-0329）。
        // 首次调用记一条，带操作 ID、调用与错误码，不带响应的内容；重试回放记下的错误、不再编码，不再记
        using var logs = new LogCapture();
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<TicketsFeature>(), services: s => s.AddSingleton<ILoggerFactory>(logs));

        var first = await host.InvokeOperationAsync<TicketRequest, TicketReply>(TicketsFeature.Create, new TicketRequest(TicketsOperation.Unencodable), Key("unencodable-logged"));
        var second = await host.InvokeOperationRawAsync(Request(TicketsFeature.Create, TicketsOperation.Unencodable), Key("unencodable-logged"));

        AssertUnencodable(first.Error);
        AssertUnencodable(second.Outcome.Error);
        var entry = Assert.Single(logs.Entries, static e => e.Level == LogLevel.Error);
        Assert.Equal(typeof(IdempotencyMiddleware).FullName, entry.Category);
        Assert.Matches($"^The response of operation '{TicketsFeature.Create}' could not be written for the idempotency record; invocation '[0-9a-f]{{32}}' ends with '{KernelErrors.Unexpected}', and retries replay it\\.$", entry.Message);
        Assert.IsType<ArgumentException>(entry.Exception);
    }

    /// <summary>
    /// 断言是写不出响应的错误
    /// </summary>
    /// <remarks>
    /// 与 XGI 入口写不出时相同：没有参数、不带 <c>inner</c>
    /// </remarks>
    private static void AssertUnencodable(Error? error)
    {
        Assert.NotNull(error);
        Assert.Equal((KernelErrors.Unexpected, KernelMessages.UnhandledException), (error.Code, error.MessageKey));
        Assert.True(error.Details is null || error.Details.Count == 0, error.ToString());
        Assert.Null(error.Inner);
    }

    /// <summary>
    /// 断言存储里的记录已经由「处理中」换成写不出的失败记录
    /// </summary>
    private static async Task AssertRecordedFailureAsync(TestHost host, string key)
    {
        var record = (await host.Services.GetRequiredService<ISharedCache>().Store.GetAsync(new CacheKey("idem:-:-:" + key), CancellationToken.None)).ValueOrThrow();
        using var document = JsonDocument.Parse(record);
        Assert.Equal("completed", document.RootElement.GetProperty("state").GetString());
        Assert.Equal(KernelErrors.Unexpected, document.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(KernelMessages.UnhandledException, document.RootElement.GetProperty("errorMessageKey").GetString());
        Assert.Equal(TicketsFeature.Create, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("response").ValueKind);
    }

    /// <summary>
    /// 启动装配了内存缓存（含幂等中间件）与取号操作的宿主
    /// </summary>
    private static Task<TestHost> StartAsync() => TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<TicketsFeature>());

    /// <summary>
    /// 只带幂等键的 Baggage
    /// </summary>
    private static Dictionary<string, string> Key(string key) => new(StringComparer.Ordinal) { [BaggageKeys.IdempotencyKey] = key };

    /// <summary>
    /// 取号的 XGI 请求
    /// </summary>
    private static OperationRequest Request(string operationId, string name)
        => new(operationId, JsonSerializer.SerializeToUtf8Bytes(new TicketRequest(name), TicketsJsonContext.Default.TicketRequest));

    /// <summary>
    /// 解码 XGI 响应载荷
    /// </summary>
    private static TicketReply? Decode(ReadOnlyMemory<byte> payload) => JsonSerializer.Deserialize(payload.Span, TicketsJsonContext.Default.TicketReply);
}
