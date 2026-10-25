// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// <see cref="IdempotencyMiddleware"/> 的单元测试：首次执行与回放、处理中冲突、异常清理、租户与主体隔离、键校验。
/// </summary>
public sealed class IdempotencyMiddlewareTests
{
    /// <summary>
    /// 承载幂等记录的假存储，可直接检查写入的键。
    /// </summary>
    private readonly FakeCacheStore _store = new();

    /// <summary>
    /// 使用默认选项的被测中间件。
    /// </summary>
    private readonly IdempotencyMiddleware _middleware = new(new IdempotencyOptions());

    /// <summary>
    /// 只注册了 <see cref="ISharedCache"/> 的服务提供者。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 处理器实际执行的次数。
    /// </summary>
    private int _executions;

    /// <summary>
    /// 用假存储与反射版序列化器装配共享缓存。
    /// </summary>
    public IdempotencyMiddlewareTests()
    {
        _services = new ServiceCollection().AddSingleton<ISharedCache>(new SerializingCache(_store, JsonCacheSerializer.CreateReflectionBased())).BuildServiceProvider();
    }

    /// <summary>
    /// 创建调用上下文；<paramref name="key"/> 不为 <see langword="null"/> 时放入幂等键 Baggage，<paramref name="tenant"/> 不为 <see langword="null"/> 时设置租户。
    /// </summary>
    private InvocationContext Context(string? key, string? tenant = null)
    {
        var context = TestInvocationContext.Create(_services, baggage: key is null ? null : new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = key });
        if (tenant is not null)
        {
            context.SetTenant(new TenantContext(tenant));
        }

        return context;
    }

    /// <summary>
    /// 只计数、不设置结果的处理器。
    /// </summary>
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
        var failure = Error.Literal("ORDERS_VALIDATION_001", "Insufficient stock.");
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
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _middleware.InvokeAsync(Context("boom"), static _ => throw new InvalidOperationException("The handler crashed.")));
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

        var restored = IdempotencyRecord.From(Error.Literal("ORDERS_NOTFOUND_001", "The order does not exist.")).ToResult();
        Assert.Equal("ORDERS_NOTFOUND_001", restored.Error?.Code);
        Assert.Equal("The order does not exist.", restored.Error?.Message);
    }
}
