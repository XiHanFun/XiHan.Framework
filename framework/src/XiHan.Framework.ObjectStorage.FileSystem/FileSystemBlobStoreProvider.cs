// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 命名存储实例的文件系统 Provider（名 <c>filesystem</c>）
/// </summary>
/// <remarks>
/// 设置的键同 <see cref="FileSystemObjectStorageOptions"/>，<c>RootPath</c> 必填（各实例各用各的目录，与缺省实例的目录也不要重叠）。建出时即建根目录并扫描已用量；探测看总配额是否还有余量，与缺省实例的健康项同一口径。预签名地址由宿主代收发（<c>HostedBlobUrlSigner.ForStore</c>）
/// </remarks>
/// <param name="clock">时钟</param>
/// <param name="loggerFactory">日志工厂</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class FileSystemBlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory) : IBlobStoreProvider
{
    /// <summary>
    /// Provider 名
    /// </summary>
    public const string ProviderName = "filesystem";

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>
    /// 日志工厂
    /// </summary>
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async ValueTask<Result<IBlobStore>> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        FileSystemObjectStorageOptions options;
        try
        {
            options = FileSystemObjectStorageOptions.FromSettings(definition.Settings);
        }
        catch (ArgumentException exception)
        {
            return BlobStoreFailures.DefinitionInvalid(definition, exception.Message);
        }

        var store = new FileSystemBlobStore(options, _clock, _loggerFactory);
        try
        {
            // 初始化扫描目录得到已用量：目录无权限、磁盘错误是后端此刻建不出来，不是定义的问题
            await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 结果里只写异常类型（消息常带完整路径），完整异常进日志
            LogInitializeFailed(_loggerFactory.CreateLogger<FileSystemBlobStoreProvider>(), definition.Name, exception);
            return BlobStoreFailures.Unavailable(exception);
        }

        return store;
    }

    /// <summary>
    /// 初始化存储目录失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Initializing file system blob store '{Store}' failed.")]
    private static partial void LogInitializeFailed(ILogger logger, string store, Exception exception);

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
    {
        var fileSystem = store as FileSystemBlobStore ?? throw new ArgumentException("The store was not created by the file system provider.", nameof(store));
        var status = fileSystem.Check()[0];
        return ValueTask.FromResult(status.State == LifecycleState.Ready ? Result.Success() : Result.Failure(status.Error!));
    }
}
