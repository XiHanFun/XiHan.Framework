// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database;

/// <summary>
/// 数据库租户目录选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:MultiTenancy:Database</c> 显式读取：<c>CacheSize</c>、<c>CacheTimeToLiveSeconds</c>（0 不过期）、<c>MaxListedTenants</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseMultiTenancyOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:MultiTenancy:Database";

    /// <summary>
    /// <see cref="DatabaseTenantStore"/> 的缓存容量（租户数，不存在的租户也占一条）。
    /// </summary>
    /// <remarks>
    /// 默认 10000，满时整体清空。
    /// </remarks>
    public int CacheSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache size must be at least 1.");
    } = 10_000;

    /// <summary>
    /// 缓存条目的存活时间。
    /// </summary>
    /// <remarks>
    /// 默认 30 秒，<see langword="null"/> 不过期（只靠 <see cref="DatabaseTenantStore.Invalidate"/> 失效）。
    /// </remarks>
    public TimeSpan? CacheTimeToLive
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <see cref="DatabaseTenantStore.ListAsync"/> 返回的租户数上限（有界枚举）。
    /// </summary>
    /// <remarks>
    /// 默认 10000，超出的部分不返回并记警告。
    /// </remarks>
    public int MaxListedTenants
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of listed tenants must be at least 1.");
    } = 10_000;

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static DatabaseMultiTenancyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseMultiTenancyOptions();
        if (configuration[$"{ConfigurationSection}:CacheSize"] is { } size)
        {
            options = int.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1
                ? options with { CacheSize = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:CacheSize must be a positive integer, but was '{size}'.");
        }

        if (configuration[$"{ConfigurationSection}:CacheTimeToLiveSeconds"] is { } ttl)
        {
            // 0 表示不过期；负数与非数字是配置错误
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? options with { CacheTimeToLive = seconds == 0 ? null : TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:CacheTimeToLiveSeconds must be a non-negative number, but was '{ttl}'.");
        }

        if (configuration[$"{ConfigurationSection}:MaxListedTenants"] is { } max)
        {
            options = int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1
                ? options with { MaxListedTenants = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:MaxListedTenants must be a positive integer, but was '{max}'.");
        }

        return options;
    }
}
