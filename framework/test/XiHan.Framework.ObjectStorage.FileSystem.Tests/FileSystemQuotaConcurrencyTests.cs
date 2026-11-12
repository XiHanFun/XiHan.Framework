// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 并发写入时的配额（ADR-0183）
/// </summary>
/// <remarks>
/// 写入边复制边在锁内预留额度，提交时按磁盘上实际的旧长度计算差值：并发写入不能合计超过总配额，并发覆盖与删除不会少记用量。内容流在交完全部字节之后挂在闸门上，所有写入方都复制完、谁也没提交，由用例放行，次序不靠运气
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
