// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core Provider 选项。从配置节 <c>XiHan:Persistence:EntityFrameworkCore</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record EntityFrameworkCorePersistenceOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:EntityFrameworkCore";

    /// <summary>
    /// 启动时调用 <c>EnsureCreated</c> 建表。只用于开发与测试；生产用迁移。默认 <see langword="false"/>。
    /// </summary>
    public bool EnsureCreated { get; init; }

    /// <summary>
    /// 健康项 <c>persistence.entityframeworkcore.database</c> 的重探间隔（也是单次探测的超时）；默认 15 秒。
    /// </summary>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置读取：<c>EnsureCreated</c>、<c>HealthProbeIntervalSeconds</c>。
    /// </summary>
    public static EntityFrameworkCorePersistenceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new EntityFrameworkCorePersistenceOptions();
        var raw = configuration[$"{ConfigurationSection}:EnsureCreated"];
        if (raw is not null)
        {
            options = bool.TryParse(raw, out var ensureCreated)
                ? options with { EnsureCreated = ensureCreated }
                : throw new ArgumentException($"'{ConfigurationSection}:EnsureCreated' must be 'true' or 'false', but was '{raw}'.");
        }

        var probe = configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"];
        if (probe is not null)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"'{ConfigurationSection}:HealthProbeIntervalSeconds' must be a positive number, but was '{probe}'.");
        }

        return options;
    }
}
