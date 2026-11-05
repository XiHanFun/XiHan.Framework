// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// IO 失败的错误（RFC-0012、ADR-0115）
/// </summary>
/// <remarks>
/// IO 异常的消息常带服务器上的完整路径：错误里的 <c>reason</c> 只写异常类型，完整异常进日志
/// </remarks>
public sealed class FileSystemBlobStoreErrorTests : IDisposable
{
    /// <summary>
    /// 存储根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task IoFailure_ReportsOnlyTheExceptionType_AndLogsTheFullException()
    {
        // 容器目录的位置上是一个文件：写入时建目录必然失败，异常消息里带完整路径
        await File.WriteAllTextAsync(Path.Combine(_root.Path, "docs"), "not a directory");
        var logs = new LogCapture();
        var store = new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = _root.Path }, TimeProvider.System, logs);

        var failed = await store.PutAsync("docs", "a.txt", Encoding.UTF8.GetBytes("hello"), BlobPutOptions.Default, CancellationToken.None);

        var logged = Assert.Single(logs.Entries, static entry => entry.Category == typeof(FileSystemBlobStore).FullName);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.NotNull(logged.Exception);
        Assert.NotNull(failed.Error);
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, failed.Error.Code);
        Assert.Equal(logged.Exception.GetType().FullName, failed.Error.Details?["reason"]);
        Assert.DoesNotContain(_root.Path, failed.Error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
