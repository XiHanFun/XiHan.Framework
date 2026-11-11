// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 包外实现分布式锁：派生 <see cref="LockHandle"/>，释放只执行一次
/// </summary>
public sealed class CustomLockHandleTests
{
    [Fact]
    public async Task ThirdPartyLock_DerivesTheHandle_AndReleaseRunsOnce()
    {
        var locks = new CountingLock();
        var held = (await locks.TryAcquireAsync(new CacheKey("orders:1"), TimeSpan.FromSeconds(5), CancellationToken.None)).ValueOrThrow();

        // 并发释放与 Dispose 一起来，底层释放也只发生一次
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => held.ReleaseAsync(CancellationToken.None).AsTask()));
        await held.DisposeAsync();

        Assert.Equal(1, results.Count(static released => released));
        Assert.Equal(1, locks.Releases);
        Assert.Equal("orders:1", held.Resource.Value);
        Assert.Throws<ArgumentException>(static () => new CountingLock.Handle(new CountingLock(), default, "t"));
        Assert.Throws<ArgumentException>(static () => new CountingLock.Handle(new CountingLock(), new CacheKey("a"), " "));
    }

    /// <summary>
    /// 记下底层释放次数的锁
    /// </summary>
    private sealed class CountingLock : IDistributedLock
    {
        /// <summary>
        /// 底层释放的次数
        /// </summary>
        private int _releases;

        /// <summary>
        /// 底层释放的次数
        /// </summary>
        public int Releases => Volatile.Read(ref _releases);

        /// <inheritdoc />
        public ValueTask<Maybe<LockHandle>> TryAcquireAsync(CacheKey resource, TimeSpan timeToLive, CancellationToken cancellationToken)
            => ValueTask.FromResult(Maybe.Some<LockHandle>(new Handle(this, resource, Guid.NewGuid().ToString("N"))));

        /// <summary>
        /// 本锁的句柄
        /// </summary>
        internal sealed class Handle(CountingLock owner, CacheKey resource, string token) : LockHandle(resource, token)
        {
            /// <inheritdoc />
            protected override async ValueTask<bool> ReleaseCoreAsync(CancellationToken cancellationToken)
            {
                // 让并发的释放有机会交错：基类的原子置位必须挡住第二次
                await Task.Yield();
                Interlocked.Increment(ref owner._releases);
                return true;
            }
        }
    }
}
