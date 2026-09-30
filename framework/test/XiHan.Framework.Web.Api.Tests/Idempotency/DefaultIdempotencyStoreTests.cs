// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 进程内幂等存储测试
/// </summary>
public class DefaultIdempotencyStoreTests
{
    private static readonly IdempotencyRecordKey Key = new("", "42", "POST", "/api/orders", "k1");
    private readonly ManualTimeProvider _clock = new();

    private DefaultIdempotencyStore CreateStore(Action<XiHanIdempotencyOptions>? configure = null)
    {
        var options = new XiHanIdempotencyOptions();
        configure?.Invoke(options);
        return new DefaultIdempotencyStore(Options.Create(options), _clock);
    }

    /// <summary>
    /// 首次取得成功，同内容再次取得为处理中
    /// </summary>
    [Fact]
    public async Task TryAcquire_SecondTimeWhileProcessing_ReturnsInProgress()
    {
        var store = CreateStore();

        var first = await store.TryAcquireAsync(Key, "fp", isTransactional: true);
        var second = await store.TryAcquireAsync(Key, "fp", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, first.Status);
        Assert.NotEqual(Guid.Empty, first.OwnerToken);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, second.Status);
    }

    /// <summary>
    /// 相同键、不同内容为冲突
    /// </summary>
    [Fact]
    public async Task TryAcquire_DifferentFingerprint_ReturnsConflict()
    {
        var store = CreateStore();
        await store.TryAcquireAsync(Key, "fp-1", true);

        var result = await store.TryAcquireAsync(Key, "fp-2", true);

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    /// <summary>
    /// 完成后重播已保存的响应
    /// </summary>
    [Fact]
    public async Task TryAcquire_AfterComplete_ReturnsReplay()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        var response = new StoredResponse(201, "{\"id\":1}"u8.ToArray());

        await store.CompleteAsync(Key, acquired.OwnerToken, response);
        var replay = await store.TryAcquireAsync(Key, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Equal(201, replay.Response!.StatusCode);
        Assert.Equal(response.Body, replay.Response.Body);
    }

    /// <summary>
    /// 完成记录超过保留期后可重新取得
    /// </summary>
    [Fact]
    public async Task TryAcquire_CompletedExpired_ReturnsAcquired()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, null));

        _clock.Advance(TimeSpan.FromHours(24));
        var result = await store.TryAcquireAsync(Key, "fp-new", true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
    }

    /// <summary>
    /// 处理中与不确定记录不自动过期
    /// </summary>
    [Fact]
    public async Task ProcessingAndIndeterminate_NeverExpire()
    {
        var store = CreateStore();
        var otherKey = Key with { Key = "k2" };
        await store.TryAcquireAsync(Key, "fp", true);
        var second = await store.TryAcquireAsync(otherKey, "fp", false);
        await store.MarkIndeterminateAsync(otherKey, second.OwnerToken);

        _clock.Advance(TimeSpan.FromDays(30));

        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);
        Assert.Equal(IdempotencyAcquireStatus.Indeterminate, (await store.TryAcquireAsync(otherKey, "fp", false)).Status);
    }

    /// <summary>
    /// 释放后可重新取得，释放会撤掉已完成记录
    /// </summary>
    [Fact]
    public async Task Release_RemovesOwnedRecordInAnyState()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, null));

        await store.ReleaseAsync(Key, acquired.OwnerToken);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, (await store.TryAcquireAsync(Key, "fp", true)).Status);
    }

    /// <summary>
    /// 令牌不符时释放不生效，完成抛出异常
    /// </summary>
    [Fact]
    public async Task WrongOwnerToken_IsRejected()
    {
        var store = CreateStore();
        await store.TryAcquireAsync(Key, "fp", true);

        await store.ReleaseAsync(Key, Guid.NewGuid());

        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAsync(Key, Guid.NewGuid(), new StoredResponse(200, null)));
    }

    /// <summary>
    /// 记录数满时返回容量已满，不驱逐处理中记录；过期完成记录会被清理腾出空间
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenFull_ReturnsCapacityExceeded()
    {
        var store = CreateStore(options => options.MaxEntries = 2);
        var a = await store.TryAcquireAsync(Key with { Key = "a" }, "fp", true);
        await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true);

        var full = await store.TryAcquireAsync(Key with { Key = "c" }, "fp", true);
        await store.CompleteAsync(Key with { Key = "a" }, a.OwnerToken, new StoredResponse(200, null));
        _clock.Advance(TimeSpan.FromHours(25));
        var afterExpiry = await store.TryAcquireAsync(Key with { Key = "c" }, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, full.Status);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true)).Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, afterExpiry.Status);
    }

    /// <summary>
    /// 快照总字节达到上限时返回容量已满
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenResponseBytesFull_ReturnsCapacityExceeded()
    {
        var store = CreateStore(options => options.MaxTotalResponseBytes = 4);
        var a = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, a.OwnerToken, new StoredResponse(200, [1, 2, 3, 4]));

        var result = await store.TryAcquireAsync(Key with { Key = "k2" }, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, result.Status);
    }

    /// <summary>
    /// 20 个并发取得只有一个成功
    /// </summary>
    [Fact]
    public async Task TryAcquire_Concurrent_OnlyOneAcquired()
    {
        var store = CreateStore();
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await store.TryAcquireAsync(Key, "fp", true);
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.Status == IdempotencyAcquireStatus.Acquired);
        Assert.All(results.Where(r => r.Status != IdempotencyAcquireStatus.Acquired),
            r => Assert.Equal(IdempotencyAcquireStatus.InProgress, r.Status));
    }
}
