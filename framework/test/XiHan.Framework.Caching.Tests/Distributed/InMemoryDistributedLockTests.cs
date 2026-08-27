// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 进程内分布式锁回退实现测试
/// </summary>
/// <remarks>
/// 只断言互斥、过期接管、幂等释放与续期这四类语义契约，不锁死内部数据结构。
/// 该实现明确只在单进程内互斥，测试也只在单进程范围内验证。
/// </remarks>
public class InMemoryDistributedLockTests
{
    private static readonly TimeSpan LongExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 资源空闲时可获取到锁句柄
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenFree_ReturnsHandle()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await using var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(handle);
        Assert.Equal("resource", handle.ResourceKey);
        Assert.False(string.IsNullOrWhiteSpace(handle.LockId));
        Assert.False(handle.IsReleased);
    }

    /// <summary>
    /// 同一资源被持有时再次获取失败且不阻塞
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenHeld_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var first = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    /// <summary>
    /// 不同资源之间互不影响
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_ForDifferentResources_BothSucceed()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await using var first = await distributedLock.TryAcquireAsync("a", LongExpiry, token);
        await using var second = await distributedLock.TryAcquireAsync("b", LongExpiry, token);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    /// <summary>
    /// 资源键两端空白被裁剪，裁剪后同名的资源互斥
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_TrimsResourceKey()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await using var first = await distributedLock.TryAcquireAsync("  resource  ", LongExpiry, token);
        var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(first);
        Assert.Equal("resource", first.ResourceKey);
        Assert.Null(second);
    }

    /// <summary>
    /// 释放后可被再次获取
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_AfterRelease_Succeeds()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var first = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(first);

        await first.ReleaseAsync();

        await using var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.True(first.IsReleased);
        Assert.NotNull(second);
        Assert.NotEqual(first.LockId, second.LockId);
    }

    /// <summary>
    /// 同步释放同样解锁资源
    /// </summary>
    [Fact]
    public async Task Dispose_ReleasesLock()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var first = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(first);

        first.Dispose();

        await using var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(second);
    }

    /// <summary>
    /// 重复释放是幂等的，不会误删接管者的锁
    /// </summary>
    /// <remarks>
    /// 释放按句柄自身持有的条目做引用比对，所以先释放再被别人接管后，重复释放不能把别人的锁删掉。
    /// </remarks>
    [Fact]
    public async Task ReleaseAsync_CalledTwice_DoesNotRevokeTakenOverLock()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var first = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(first);
        await first.ReleaseAsync();

        await using var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        await first.ReleaseAsync();

        Assert.NotNull(second);
        Assert.False(second.IsReleased);
        Assert.Null(await distributedLock.TryAcquireAsync("resource", LongExpiry, token));
    }

    /// <summary>
    /// 锁过期后允许其他调用方接管
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task TryAcquireAsync_AfterExpiry_AllowsTakeOver()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var first = await distributedLock.TryAcquireAsync("resource", TimeSpan.FromMilliseconds(1), token);
        Assert.NotNull(first);

        await Task.Delay(120, token);

        await using var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(second);
        Assert.NotEqual(first.LockId, second.LockId);
    }

    /// <summary>
    /// 持有期间续期成功
    /// </summary>
    [Fact]
    public async Task ExtendAsync_WhileHeld_ReturnsTrue()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(handle);

        Assert.True(await handle.ExtendAsync(TimeSpan.FromMinutes(10), token));
    }

    /// <summary>
    /// 释放后续期失败
    /// </summary>
    [Fact]
    public async Task ExtendAsync_AfterRelease_ReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(handle);
        await handle.ReleaseAsync();

        Assert.False(await handle.ExtendAsync(TimeSpan.FromMinutes(10), token));
    }

    /// <summary>
    /// 锁已被他人接管后续期失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ExtendAsync_AfterTakenOver_ReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var first = await distributedLock.TryAcquireAsync("resource", TimeSpan.FromMilliseconds(1), token);
        Assert.NotNull(first);

        await Task.Delay(120, token);
        await using var second = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(second);

        Assert.False(await first.ExtendAsync(LongExpiry, token));
    }

    /// <summary>
    /// 续期时长必须为正
    /// </summary>
    [Fact]
    public async Task ExtendAsync_WithNonPositiveExpiry_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(handle);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handle.ExtendAsync(TimeSpan.Zero, token));
    }

    /// <summary>
    /// 资源键为空时拒绝获取
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryAcquireAsync_WithBlankResourceKey_Throws(string? resourceKey)
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => distributedLock.TryAcquireAsync(resourceKey!, LongExpiry, token));
    }

    /// <summary>
    /// 过期时长必须为正
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WithNonPositiveExpiry_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => distributedLock.TryAcquireAsync("resource", TimeSpan.Zero, token));
    }

    /// <summary>
    /// 并发争抢同一资源时只有一个调用方拿到锁
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task TryAcquireAsync_WithConcurrentCallers_GrantsExactlyOne()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => distributedLock.TryAcquireAsync("resource", LongExpiry, token), token))
            .ToArray();

        var handles = await Task.WhenAll(tasks);

        Assert.Equal(1, handles.Count(handle => handle is not null));

        foreach (var handle in handles)
        {
            handle?.Dispose();
        }
    }
}
