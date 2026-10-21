// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Abstractions.Tests;

public sealed class IdempotencyMiddlewareTests
{
    private readonly FakeCacheStore _store = new();
    private readonly IdempotencyMiddleware _middleware = new(new IdempotencyOptions());
    private readonly IServiceProvider _services;
    private int _executions;

    public IdempotencyMiddlewareTests()
    {
        _services = new ServiceCollection().AddSingleton<ISharedCache>(new SerializingCache(_store, JsonCacheSerializer.CreateReflectionBased())).BuildServiceProvider();
    }

    private InvocationContext Context(string? key, string? tenant = null)
    {
        var context = TestInvocationContext.Create(_services, baggage: key is null ? null : new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = key });
        if (tenant is not null)
        {
            context.SetTenant(new TenantContext(tenant));
        }

        return context;
    }

    private ValueTask Handler(InvocationContext context)
    {
        _executions++;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutKey_PassesThrough_WithoutTouchingStore()
    {
        var context = Context(key: null);

        await _middleware.InvokeAsync(context, Handler);

        Assert.Equal(1, _executions);
        Assert.Empty(_store.Operations);
        Assert.False(IdempotencyMiddleware.WasReplayed(context));
    }

    [Fact]
    public async Task FirstCall_Executes_SecondCall_ReplaysOutcome()
    {
        var failure = Error.Validation("ORDERS_VALIDATION_001", "库存不足");
        var first = Context("req-1");

        await _middleware.InvokeAsync(first, ctx =>
        {
            _executions++;
            ctx.SetOutcome(failure);
            return ValueTask.CompletedTask;
        });
        var second = Context("req-1");
        await _middleware.InvokeAsync(second, Handler);

        Assert.Equal(1, _executions);
        Assert.Equal(failure.Code, second.GetOutcome().ValueOrThrow().Error?.Code);
        Assert.Equal(failure.Message, second.GetOutcome().ValueOrThrow().Error?.Message);
        Assert.True(IdempotencyMiddleware.WasReplayed(second));
        Assert.False(IdempotencyMiddleware.WasReplayed(first));
        Assert.Equal(1, _store.Count);
        Assert.Contains("idem:-:-:req-1", _store.Operations.Select(static o => o["add:".Length..]));
    }

    [Fact]
    public async Task SuccessfulOutcome_IsReplayedAsSuccess()
    {
        await _middleware.InvokeAsync(Context("req-ok"), Handler);
        var replay = Context("req-ok");

        await _middleware.InvokeAsync(replay, Handler);

        Assert.Equal(1, _executions);
        Assert.True(replay.GetOutcome().ValueOrThrow().IsSuccess);
        Assert.True(IdempotencyMiddleware.WasReplayed(replay));
    }

    [Fact]
    public async Task InProgressMarker_ReturnsConflict_WithoutExecuting()
    {
        await _store.SetAsync(new CacheKey("idem:-:-:busy"), Encoding.UTF8.GetBytes("{\"state\":\"in-progress\"}"), CacheEntryOptions.Default, CancellationToken.None);
        var context = Context("busy");

        await _middleware.InvokeAsync(context, Handler);

        Assert.Equal(0, _executions);
        Assert.Equal(CachingErrors.IdempotentInProgress, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task Exception_ClearsMarker_AndRethrows_SoRetryExecutes()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _middleware.InvokeAsync(Context("boom"), static _ => throw new InvalidOperationException("崩溃")));
        Assert.Equal(0, _store.Count);

        await _middleware.InvokeAsync(Context("boom"), Handler);

        Assert.Equal(1, _executions);
    }

    [Fact]
    public async Task Scope_SeparatesTenantsAndPrincipals()
    {
        await _middleware.InvokeAsync(Context("shared", tenant: "t1"), Handler);
        await _middleware.InvokeAsync(Context("shared", tenant: "t2"), Handler);
        await _middleware.InvokeAsync(Context("shared", tenant: "t1"), Handler);

        var principal = Context("shared", tenant: "t1");
        principal.SetSecurity(new SecurityContext(new Principal("user-7", "test"), new TenantContext("t1")));
        await _middleware.InvokeAsync(principal, Handler);

        Assert.Equal(3, _executions);
        Assert.NotNull(_store["idem:t1:-:shared"]);
        Assert.NotNull(_store["idem:t2:-:shared"]);
        Assert.NotNull(_store["idem:t1:user-7:shared"]);
    }

    [Fact]
    public async Task InvalidKey_IsRejected_WithoutExecuting()
    {
        var context = Context("has space");

        await _middleware.InvokeAsync(context, Handler);

        Assert.Equal(0, _executions);
        Assert.Equal(CachingErrors.InvalidKey, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public void IdempotencyRecord_RoundTripsOutcomes()
    {
        Assert.True(IdempotencyRecord.From(Result.Success()).ToResult().IsSuccess);
        Assert.False(IdempotencyRecord.From(Result.Success()).IsInProgress);
        Assert.True(IdempotencyRecord.InProgress.IsInProgress);

        var restored = IdempotencyRecord.From(Error.NotFound("ORDERS_NOTFOUND_001", "没有订单")).ToResult();
        Assert.Equal("ORDERS_NOTFOUND_001", restored.Error?.Code);
        Assert.Equal("没有订单", restored.Error?.Message);
    }
}
