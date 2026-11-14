// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database;

/// <summary>
/// 数据库租户目录选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:MultiTenancy:Database</c> 显式读取：<c>CacheTimeToLiveSeconds</c>（正数）。缓存容量由提供 <c>caching.local</c> 的 Provider 管（ADR-0229）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseMultiTenancyOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:MultiTenancy:Database";

    /// <summary>
    /// <see cref="DatabaseTenantStore"/> 缓存条目的存活时间
    /// </summary>
    /// <remarks>
    /// 默认 30 秒。经 <see cref="TenantRecordStore"/> 的写入（停用、改连接串、删除）立即对所有共用缓存的实例生效，存活时间只是兜底：失效通知丢失、缓存后端在释放时的失效里故障，或绕过它直接改表时，旧租户信息最多留这么久。必须为正，不再有「不过期」（ADR-0229）
    /// </remarks>
    public TimeSpan CacheTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static DatabaseMultiTenancyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseMultiTenancyOptions();
        if (configuration[$"{ConfigurationSection}:CacheTimeToLiveSeconds"] is { } ttl)
        {
            // 0、负数与非数字都是配置错误：缓存条目必须会过期，失效丢失时才有上限
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { CacheTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:CacheTimeToLiveSeconds must be a positive number, but was '{ttl}'.");
        }

        return options;
    }
}
