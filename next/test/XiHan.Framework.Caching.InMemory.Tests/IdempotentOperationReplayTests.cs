// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory.Tests.Fixtures;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 幂等回放操作的响应（RFC-0006 幂等第 4 条，ADR-0064）。
/// </summary>
/// <remarks>
/// XGI 入口拿到首次调用的响应载荷，类型化入口拿到首次调用的响应对象，处理器只执行一次；同一个键被另一个操作或非操作调用用过、记录里没有响应时明确失败，不返回空载荷。
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

    /// <summary>
    /// 启动装配了内存缓存（含幂等中间件）与取号操作的宿主。
    /// </summary>
    private static Task<TestHost> StartAsync() => TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<TicketsFeature>());

    /// <summary>
    /// 只带幂等键的 Baggage。
    /// </summary>
    private static Dictionary<string, string> Key(string key) => new(StringComparer.Ordinal) { [BaggageKeys.IdempotencyKey] = key };

    /// <summary>
    /// 取号的 XGI 请求。
    /// </summary>
    private static OperationRequest Request(string operationId, string name)
        => new(operationId, JsonSerializer.SerializeToUtf8Bytes(new TicketRequest(name), TicketsJsonContext.Default.TicketRequest));

    /// <summary>
    /// 解码 XGI 响应载荷。
    /// </summary>
    private static TicketReply? Decode(ReadOnlyMemory<byte> payload) => JsonSerializer.Deserialize(payload.Span, TicketsJsonContext.Default.TicketReply);
}
