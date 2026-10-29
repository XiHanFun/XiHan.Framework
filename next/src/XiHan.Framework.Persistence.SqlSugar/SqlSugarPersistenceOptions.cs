// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar Provider 选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:SqlSugar</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SqlSugarPersistenceOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:SqlSugar";

    /// <summary>
    /// 启动时用 CodeFirst 为已登记的实体建表（<c>InitTables</c>）。
    /// </summary>
    /// <remarks>
    /// 只用于开发与测试；生产由应用管理表结构。默认 <see langword="false"/>。
    /// </remarks>
    public bool InitTables { get; init; }

    /// <summary>
    /// 健康项 <c>persistence.sqlsugar.database</c> 的重探间隔（也是单次探测的超时）。
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 哪些实体的仓储写入经 SqlSugar 差异日志。
    /// </summary>
    /// <remarks>
    /// <c>EnableDiffLogEvent</c>，业务数据为 <see cref="SqlSugarDiffLogData"/>。应用在连接配置的 <c>AopEvents.OnDiffLogEvent</c> 上接收。缺省一个都不开。开启的实体每次写入多一次按主键的读取（更新与删除读写前值，插入读写后值），只给需要审计前后值的实体开。不从配置读取，经 <see cref="SqlSugarPersistenceFeature.DiffLog"/> 设置。
    /// </remarks>
    public Func<Type, bool> DiffLog
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static _ => false;

    /// <summary>
    /// 从配置读取：<c>InitTables</c>、<c>HealthProbeIntervalSeconds</c>。
    /// </summary>
    public static SqlSugarPersistenceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new SqlSugarPersistenceOptions();
        var raw = configuration[$"{ConfigurationSection}:InitTables"];
        if (raw is not null)
        {
            options = bool.TryParse(raw, out var initTables)
                ? options with { InitTables = initTables }
                : throw new ArgumentException($"'{ConfigurationSection}:InitTables' must be 'true' or 'false', but was '{raw}'.");
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
