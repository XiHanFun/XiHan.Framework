// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 让 <see cref="FileSystemBlobStore"/> 跑完整套 <see cref="BlobStoreConformance"/> 用例；每个用例一个临时根目录。
/// </summary>
public sealed class FileSystemBlobStoreConformanceTests : BlobStoreConformance, IDisposable
{
    /// <summary>
    /// 本用例的临时根目录。
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    protected override IBlobStore CreateStore() => new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = _root.Path }, TimeProvider.System);

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();
}
