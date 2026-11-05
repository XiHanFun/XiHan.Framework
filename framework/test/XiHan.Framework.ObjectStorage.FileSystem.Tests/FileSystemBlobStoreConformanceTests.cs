// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 让 <see cref="FileSystemBlobStore"/> 跑完整套 <see cref="BlobStoreConformance"/> 用例
/// </summary>
/// <remarks>
/// 每个用例一个临时根目录
/// </remarks>
public sealed class FileSystemBlobStoreConformanceTests : BlobStoreConformance, IDisposable
{
    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    protected override IBlobStore CreateStore() => new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = _root.Path }, TimeProvider.System, NullLoggerFactory.Instance);

    /// <inheritdoc />
    /// <remarks>
    /// 直接写进容器的内容目录 <c>&lt;根&gt;/&lt;容器&gt;/data/</c>，各段按存储自己的编码转成文件名，不写元数据
    /// </remarks>
    protected override async Task PlantOutsideContractAsync(ContainerName container, string key, byte[] content)
    {
        var segments = key.Split(BlobKey.Separator).Select(BlobPathEncoding.EncodeSegment).ToArray();
        var directory = Path.Combine([_root.Path, container.Value, "data", .. segments[..^1]]);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, segments[^1]), content);
    }

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();
}
