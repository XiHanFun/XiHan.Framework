// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统对象存储选项。从配置节 <c>XiHan:ObjectStorage:FileSystem</c> 显式读取：<c>RootPath</c>、<c>MaxTotalBytes</c>、<c>MaxBlobBytes</c>、<c>MultipartExpiryHours</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record FileSystemObjectStorageOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:FileSystem";

    /// <summary>
    /// 根目录；默认 <c>&lt;AppContext.BaseDirectory&gt;/blobs</c>。相对路径按当前目录解析。
    /// </summary>
    public string RootPath
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? Path.GetFullPath(value) : throw new ArgumentException("The root path must not be empty.", nameof(value));
    } = Path.Combine(AppContext.BaseDirectory, "blobs");

    /// <summary>
    /// 全部对象的总字节上限；默认 1 GiB。
    /// </summary>
    public long MaxTotalBytes
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The total quota must be positive.");
    } = 1L << 30;

    /// <summary>
    /// 单个对象的字节上限；默认 256 MiB。
    /// </summary>
    public long MaxBlobBytes
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The per-blob limit must be positive.");
    } = 256L << 20;

    /// <summary>
    /// 未完成的分片上传保留多久；启动扫描时清掉更早发起的上传。默认 7 天。
    /// </summary>
    public TimeSpan MultipartExpiry
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The multipart expiry must be positive.");
    } = TimeSpan.FromDays(7);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static FileSystemObjectStorageOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new FileSystemObjectStorageOptions();
        if (configuration[$"{ConfigurationSection}:RootPath"] is { } root && !string.IsNullOrWhiteSpace(root))
        {
            options = options with { RootPath = root };
        }

        var total = configuration[$"{ConfigurationSection}:MaxTotalBytes"];
        if (total is not null)
        {
            options = long.TryParse(total, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxTotalBytes = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxTotalBytes must be a positive integer, but was '{total}'.");
        }

        var blob = configuration[$"{ConfigurationSection}:MaxBlobBytes"];
        if (blob is not null)
        {
            options = long.TryParse(blob, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxBlobBytes = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxBlobBytes must be a positive integer, but was '{blob}'.");
        }

        var expiry = configuration[$"{ConfigurationSection}:MultipartExpiryHours"];
        if (expiry is not null)
        {
            options = double.TryParse(expiry, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) && hours > 0 && !double.IsInfinity(hours)
                ? options with { MultipartExpiry = TimeSpan.FromHours(hours) }
                : throw new ArgumentException($"{ConfigurationSection}:MultipartExpiryHours must be a positive number, but was '{expiry}'.");
        }

        return options;
    }
}
