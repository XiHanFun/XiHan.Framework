// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 并发写入时的配额与元数据（ADR-0183、ADR-0238、ADR-0239）
/// </summary>
/// <remarks>
/// 写入边复制边在锁内预留额度，被总配额拒绝时在同一把锁里退还已预留的部分，提交时按磁盘上实际的旧长度计算差值：并发写入不能合计超过总配额，也不会同时被拒到一个都写不成，并发覆盖与删除不会少记用量。内容与元数据在同一个临界区里换上，并发覆盖同一个键留下的元数据属于盘上的内容。内容流在交完全部字节之后挂在闸门上，等每个写入方要么停在提交之前、要么已被拒，再由用例放行，次序不靠运气
/// </remarks>
public sealed class FileSystemQuotaConcurrencyTests : IDisposable
{
    /// <summary>
    /// 容器
    /// </summary>
    private static readonly ContainerName _container = new("quota");

    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task ConcurrentWrites_CannotTogetherExceedTheTotalQuota()
    {
        const long quota = 1024 * 1024;
        const int each = 768 * 1024;
        var store = Store(quota);
        var gate = new Gate();

        var writes = Enumerable.Range(0, 8).Select(i => store.PutAsync(_container, new BlobKey($"k{i}"), gate.Stream(each), BlobPutOptions.Default, CancellationToken.None).AsTask()).ToList();
        await gate.WaitUntilSettledAsync(writes);
        gate.Open();
        var results = await Task.WhenAll(writes);

        var written = results.Where(static r => r.IsSuccess).Sum(static r => r.ValueOrThrow().Length);
        Assert.InRange(written, each, quota);
        Assert.All(results.Where(static r => r.IsFailure), static r => Assert.Equal(ObjectStorageErrors.QuotaExceeded, r.Error!.Code));
        Assert.Equal(written, store.UsedBytes);
        Assert.Equal(written, DiskBytes());
    }

    [Fact]
    public async Task ConcurrentWritesThatRunOutOfQuotaTogether_LetOneOfThemFinish()
    {
        // 8 个写入各预留两段（128 KiB）后会合，合起来正好占满 1 MiB，再同时继续：下一段谁都预留不到；
        // 修之前预留不到的写入方关掉临时文件之后才在 finally 里退还预留，同时被拒的几个互相看到对方还没退的额度，一个都写不成
        // （负载下的 ConcurrentWrites_CannotTogetherExceedTheTotalQuota 也因此偶发失败）
        const long quota = 1024 * 1024;
        const int each = 768 * 1024;
        var store = Store(quota);
        for (var round = 0; round < LockstepRounds; round++)
        {
            var lockstep = new Lockstep(participants: 8, stallAt: 2 * Lockstep.Chunk);
            var keys = Enumerable.Range(0, 8).Select(i => new BlobKey($"r{round}-k{i}")).ToList();

            var results = await Task.WhenAll(keys.Select(key => store.PutAsync(_container, key, lockstep.Stream(each), BlobPutOptions.Default, CancellationToken.None).AsTask()));

            var written = results.Where(static r => r.IsSuccess).Sum(static r => r.ValueOrThrow().Length);
            Assert.InRange(written, each, quota);
            Assert.All(results.Where(static r => r.IsFailure), static r => Assert.Equal(ObjectStorageErrors.QuotaExceeded, r.Error!.Code));
            Assert.Equal(written, store.UsedBytes);
            Assert.Equal(written, DiskBytes());

            // 下一轮从空的容器开始
            Assert.All(await store.DeleteAsync(_container, keys, CancellationToken.None), static r => r.ValueOrThrow());
            Assert.Equal(0, store.UsedBytes);
        }
    }

    [Fact]
    public async Task ConcurrentOverwritesOfOneKey_CountTheBytesThatAreActuallyOnDisk()
    {
        var store = Store(1L << 30);
        var key = new BlobKey("shared");
        (await store.PutAsync(_container, key, new byte[512 * 1024], BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var gate = new Gate();

        var writes = Enumerable.Range(0, 2).Select(_ => store.PutAsync(_container, key, gate.Stream(100 * 1024), BlobPutOptions.Default, CancellationToken.None).AsTask()).ToList();
        await gate.WaitUntilSettledAsync(writes);
        gate.Open();
        foreach (var result in await Task.WhenAll(writes))
        {
            result.ValueOrThrow();
        }

        // 修之前两个覆盖都按开始时读到的旧长度 512 KiB 扣减，用量记成负数，之后的写入不受配额约束
        Assert.Equal(100 * 1024, store.UsedBytes);
        Assert.Equal(100 * 1024, DiskBytes());
    }

    [Fact]
    public async Task ConcurrentOverwritesOfOneKey_KeepTheMetadataOfTheContentOnDisk()
    {
        // 后换上内容的写入方也要后换上元数据。修之前元数据在配额锁外改名：A 先换上内容、停在取修改时间的那一下，
        // B 随后换上内容并写完元数据，A 再写，留下 B 的内容与 A 的长度、ETag；
        // 负载下两个元数据改名同时替换同一个目标，Windows 还会以拒绝访问让其中一个失败（ADR-0239）
        using var clock = new PausingClock();
        var store = new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = _root.Path, MaxTotalBytes = 1L << 30, MaxBlobBytes = 1L << 30 }, clock, NullLoggerFactory.Instance);
        var key = new BlobKey("contested");
        (await store.PutAsync(_container, key, new byte[512 * 1024], BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var first = new Gate();
        var second = new Gate();
        var a = store.PutAsync(_container, key, first.Stream(100 * 1024), BlobPutOptions.Default, CancellationToken.None).AsTask();
        var b = store.PutAsync(_container, key, second.Stream(200 * 1024), BlobPutOptions.Default, CancellationToken.None).AsTask();
        await first.WaitUntilSettledAsync<Primitives.Result<BlobMetadata>>([a]);
        await second.WaitUntilSettledAsync<Primitives.Result<BlobMetadata>>([b]);

        // 先放行 A，等它停在提交途中取修改时间的那一下，再放行 B、等 B 写完，最后让 A 继续
        clock.PauseNextRead();
        first.Open();
        try
        {
            await clock.WaitUntilPausedAsync(a);
            second.Open();
            await Task.WhenAny(b, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
        finally
        {
            clock.Resume();
        }

        var results = await Task.WhenAll(a, b);

        Assert.All(results, static r => Assert.True(r.IsSuccess, r.Error?.ToString()));
        var head = (await store.HeadAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        var content = (await store.GetAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        byte[] bytes;
        await using (content)
        {
            using var copy = new MemoryStream();
            await content.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);
            bytes = copy.ToArray();
        }

        Assert.Equal(bytes.Length, head.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), head.ETag);
        Assert.Equal(bytes.Length, store.UsedBytes);
        Assert.Equal(bytes.Length, DiskBytes());
    }

    [Fact]
    public async Task DeleteDuringAnOverwrite_CountsTheBytesThatAreActuallyOnDisk()
    {
        var store = Store(1L << 30);
        var key = new BlobKey("doomed");
        (await store.PutAsync(_container, key, new byte[512 * 1024], BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var gate = new Gate();

        var write = store.PutAsync(_container, key, gate.Stream(100 * 1024), BlobPutOptions.Default, CancellationToken.None).AsTask();
        await gate.WaitUntilSettledAsync<Primitives.Result<BlobMetadata>>([write]);
        Assert.True((await store.DeleteAsync(_container, [key], CancellationToken.None)).Single().ValueOrThrow());
        gate.Open();
        (await write).ValueOrThrow();

        Assert.Equal(100 * 1024, store.UsedBytes);
        Assert.Equal(100 * 1024, DiskBytes());
    }

    [Fact]
    public async Task OverwritingNeedsRoomForBothVersions_WhileTheNewOneIsBeingWritten()
    {
        // 复制期间新旧内容同时在盘上：预留按新内容的全长，旧对象的字节到提交时才释放
        var store = Store(1024 * 1024);
        var key = new BlobKey("big");
        (await store.PutAsync(_container, key, new byte[600 * 1024], BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var replaced = await store.PutAsync(_container, key, new byte[600 * 1024], BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.QuotaExceeded, replaced.Error?.Code);
        Assert.Equal(600 * 1024, store.UsedBytes);
        Assert.True((await store.PutAsync(_container, key, new byte[300 * 1024], BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(300 * 1024, store.UsedBytes);
    }

    /// <summary>
    /// 以给定总配额创建存储
    /// </summary>
    private FileSystemBlobStore Store(long maxTotal)
        => new(new FileSystemObjectStorageOptions { RootPath = _root.Path, MaxTotalBytes = maxTotal, MaxBlobBytes = maxTotal }, new FakeTimeProvider(), NullLoggerFactory.Instance);

    /// <summary>
    /// 容器里对象内容实际占的字节数
    /// </summary>
    private long DiskBytes()
    {
        var data = Path.Combine(_root.Path, _container.Value, "data");
        return Directory.Exists(data) ? Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Sum(static f => new FileInfo(f).Length) : 0;
    }

    /// <summary>
    /// 会合用例重复的轮数
    /// </summary>
    /// <remarks>
    /// 修之前的缺陷要几个写入方恰好同时被拒才出现，单轮不一定碰上，多跑几轮把它稳定地暴露出来；修之后每一轮都确定通过
    /// </remarks>
    private const int LockstepRounds = 5;

    /// <summary>
    /// 一组内容流共用的会合点
    /// </summary>
    /// <remarks>
    /// 每个流交出 <c>stallAt</c> 个字节后停下，全部到齐才一起交出下一段，让各写入方的下一次预留几乎同时发生
    /// </remarks>
    private sealed class Lockstep(int participants, int stallAt)
    {
        /// <summary>
        /// 每次读取至多交出的字节数，与存储复制时的缓冲区一样大
        /// </summary>
        public const int Chunk = 64 * 1024;

        /// <summary>
        /// 全部到齐
        /// </summary>
        private readonly TaskCompletionSource _together = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 已到会合点的流数
        /// </summary>
        private int _arrived;

        /// <summary>
        /// 会合点：每个流交出这么多字节后停下
        /// </summary>
        private int Stall => stallAt;

        /// <summary>
        /// 给出 <paramref name="length"/> 个字节、中途在会合点停一次的流
        /// </summary>
        public Stream Stream(int length) => new LockstepStream(this, length);

        /// <summary>
        /// 到会合点：最后一个到的放行全部
        /// </summary>
        private Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) == participants)
            {
                _together.TrySetResult();
            }

            return _together.Task;
        }

        /// <summary>
        /// 中途在会合点停一次的只读流
        /// </summary>
        private sealed class LockstepStream(Lockstep lockstep, int length) : System.IO.Stream
        {
            /// <summary>
            /// 已交出的字节数
            /// </summary>
            private int _served;

            /// <summary>
            /// 是否已在会合点停过
            /// </summary>
            private bool _arrived;

            /// <inheritdoc />
            public override bool CanRead => true;

            /// <inheritdoc />
            public override bool CanSeek => false;

            /// <inheritdoc />
            public override bool CanWrite => false;

            /// <inheritdoc />
            public override long Length => throw new NotSupportedException();

            /// <inheritdoc />
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            /// <inheritdoc />
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            /// <inheritdoc />
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_served == lockstep.Stall && !_arrived)
                {
                    _arrived = true;
                    await lockstep.ArriveAsync().WaitAsync(cancellationToken);
                }

                // 不越过会合点：之前的每次读取都在它之前结束，停下时已交出的正好是 stallAt 个字节
                var end = _served < lockstep.Stall ? lockstep.Stall : length;
                var take = Math.Min(Math.Min(buffer.Length, Chunk), end - _served);
                buffer.Span[..take].Clear();
                _served += take;
                return take;
            }

            /// <inheritdoc />
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            /// <inheritdoc />
            public override void Flush()
            {
            }

            /// <inheritdoc />
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            /// <inheritdoc />
            public override void SetLength(long value) => throw new NotSupportedException();

            /// <inheritdoc />
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 能让下一次读数停住的时钟
    /// </summary>
    /// <remarks>
    /// 存储提交时读它取元数据的修改时间：用例借此让一个写入方停在提交途中，再让另一个写入方走完，次序不靠运气
    /// </remarks>
    private sealed class PausingClock : TimeProvider, IDisposable
    {
        /// <summary>
        /// 固定的读数
        /// </summary>
        private static readonly DateTimeOffset _now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// 停住的读数何时继续
        /// </summary>
        private readonly ManualResetEventSlim _resume = new(initialState: true);

        /// <summary>
        /// 有读数停住了
        /// </summary>
        private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 下一次读数是否要停住（1 为是）
        /// </summary>
        private int _armed;

        /// <summary>
        /// 让下一次读数停住，直到 <see cref="Resume"/>
        /// </summary>
        public void PauseNextRead()
        {
            _resume.Reset();
            Volatile.Write(ref _armed, 1);
        }

        /// <summary>
        /// 等有读数停住
        /// </summary>
        /// <remarks>
        /// <paramref name="reader"/> 先结束（不再读时钟）也不再等，断言失败
        /// </remarks>
        public async Task WaitUntilPausedAsync(Task reader)
        {
            var settled = await Task.WhenAny(_paused.Task, reader).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Same(_paused.Task, settled);
        }

        /// <summary>
        /// 让停住的读数继续，之后的读数不再停
        /// </summary>
        public void Resume()
        {
            Volatile.Write(ref _armed, 0);
            _resume.Set();
        }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            // 只停住布置之后的第一次读数：之后读时钟的写入方照常走
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                _paused.TrySetResult();
                _resume.Wait();
            }

            return _now;
        }

        /// <inheritdoc />
        public void Dispose() => _resume.Dispose();
    }

    /// <summary>
    /// 一组内容流共用的闸门
    /// </summary>
    private sealed class Gate
    {
        /// <summary>
        /// 放行
        /// </summary>
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 停在闸门上的流数
        /// </summary>
        private int _waiting;

        /// <summary>
        /// 给出 <paramref name="length"/> 个字节、读到结尾时停在闸门上的流
        /// </summary>
        public Stream Stream(int length) => new GatedStream(this, length);

        /// <summary>
        /// 放行全部
        /// </summary>
        public void Open() => _open.TrySetResult();

        /// <summary>
        /// 等每个写入要么停在闸门上、要么已经结束
        /// </summary>
        public async Task WaitUntilSettledAsync<T>(List<Task<T>> writes)
        {
            for (var attempt = 0; Volatile.Read(ref _waiting) + writes.Count(static w => w.IsCompleted) < writes.Count; attempt++)
            {
                Assert.True(attempt < 1000, "The writes did not settle.");
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 读到结尾：记一次停靠，等放行
        /// </summary>
        public Task ArriveAsync()
        {
            Interlocked.Increment(ref _waiting);
            return _open.Task;
        }

        /// <summary>
        /// 交完内容后停在闸门上的只读流
        /// </summary>
        private sealed class GatedStream(Gate gate, int length) : System.IO.Stream
        {
            /// <summary>
            /// 已交出的字节数
            /// </summary>
            private int _served;

            /// <summary>
            /// 是否已在闸门上停过
            /// </summary>
            private bool _arrived;

            /// <inheritdoc />
            public override bool CanRead => true;

            /// <inheritdoc />
            public override bool CanSeek => false;

            /// <inheritdoc />
            public override bool CanWrite => false;

            /// <inheritdoc />
            public override long Length => throw new NotSupportedException();

            /// <inheritdoc />
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            /// <inheritdoc />
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            /// <inheritdoc />
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_served < length)
                {
                    var take = Math.Min(buffer.Length, length - _served);
                    buffer.Span[..take].Clear();
                    _served += take;
                    return take;
                }

                if (!_arrived)
                {
                    _arrived = true;
                    await gate.ArriveAsync().WaitAsync(cancellationToken);
                }

                return 0;
            }

            /// <inheritdoc />
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            /// <inheritdoc />
            public override void Flush()
            {
            }

            /// <inheritdoc />
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            /// <inheritdoc />
            public override void SetLength(long value) => throw new NotSupportedException();

            /// <inheritdoc />
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
