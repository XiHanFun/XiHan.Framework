// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 让 <see cref="FileSystemBlobStoreProvider"/> 跑完整套 <see cref="BlobStoreProviderConformance"/> 用例。
/// </summary>
/// <remarks>
/// 两组设置各用临时根目录下的一个子目录。
/// </remarks>
public sealed class FileSystemBlobStoreProviderConformanceTests : BlobStoreProviderConformance, IDisposable
{
    /// <summary>
    /// 本用例的临时根目录。
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    protected override IBlobStoreProvider CreateProvider() => new FileSystemBlobStoreProvider(TimeProvider.System);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> ValidSettings() => new Dictionary<string, string> { ["RootPath"] = Path.Combine(_root.Path, "a") };

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> OtherValidSettings() => new Dictionary<string, string> { ["rootpath"] = Path.Combine(_root.Path, "b"), ["MaxBlobBytes"] = "1048576" };

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();
}
