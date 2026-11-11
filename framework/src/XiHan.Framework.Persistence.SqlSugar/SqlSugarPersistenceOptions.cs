// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar Provider 选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:SqlSugar</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record SqlSugarPersistenceOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:SqlSugar";

    /// <summary>
    /// <see cref="CommandTimeout"/> 的缺省值
    /// </summary>
    /// <remarks>
    /// 与 SqlSugar 自己的缺省值相同，不配时行为不变
    /// </remarks>
    public static TimeSpan StandardCommandTimeout { get; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// 启动时用 CodeFirst 为已登记的实体建表（<c>InitTables</c>）
    /// </summary>
    /// <remarks>
    /// 只用于开发与测试；生产由应用管理表结构。默认 <see langword="false"/>
    /// </remarks>
    public bool InitTables { get; init; }

    /// <summary>
    /// 健康项 <c>persistence.sqlsugar.database</c> 的重探间隔（也是单次探测的超时）
    /// </summary>
    /// <remarks>
    /// 默认 15 秒
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 哪些实体的仓储写入经 SqlSugar 差异日志
    /// </summary>
    /// <remarks>
    /// <c>EnableDiffLogEvent</c>，业务数据为 <see cref="SqlSugarDiffLogData"/>。应用在连接配置的 <c>AopEvents.OnDiffLogEvent</c> 上接收。缺省一个都不开。开启的实体每次写入多一次按主键的读取（更新与删除读写前值，插入读写后值），只给需要审计前后值的实体开。不从配置读取，经 <see cref="SqlSugarPersistenceFeature.DiffLog"/> 设置
    /// </remarks>
    [ApiLevel(Stability.Experimental, "5.0")]
    public Func<Type, bool> DiffLog
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static _ => false;

    /// <summary>
    /// 方式一（特性按回调建客户端）的命名连接：名字 → 连接串
    /// </summary>
    /// <remarks>
    /// 来自 <c>XiHan:Persistence:SqlSugar:Connections:&lt;name&gt;</c>，缺省为空。每个作用域的客户端都带上它们（<c>ConfigId</c> 是名字，排在默认连接之后），供登记到命名连接的实体与绑定连接的工作单元使用（RFC-0005「命名连接」）。名字合 <c>^[a-z0-9][a-z0-9-]{0,63}$</c>，不能是 <see cref="SqlSugarConnections.Default"/> 或 <see cref="SqlSugarConnections.Shard"/>；连接串是机密，不进日志
    /// </remarks>
    public IReadOnlyDictionary<string, string> Connections
    {
        get => field;
        init => field = ValidateConnections(value);
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 命令超时
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="StandardCommandTimeout"/>。特性建的客户端的每个连接都设上；应用自己注册客户端时不适用。必须是正数：ADO.NET 的 0 是无限等待。个别长任务在调用点用 <c>Ado.CommandTimeOut</c> 临时调大
    /// 至少 1 秒，按整秒向上取整交给 ADO.NET；与同类型其他时长一样是 <see cref="TimeSpan"/>，配置键仍是 <c>CommandTimeoutSeconds</c>（秒，ADR-0164）
    /// </remarks>
    public TimeSpan CommandTimeout
    {
        get => field;
        init => field = value >= TimeSpan.FromSeconds(1) && value.TotalSeconds <= int.MaxValue ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The command timeout must be at least one second.");
    } = StandardCommandTimeout;

    /// <summary>
    /// 从库的权重与健康探测
    /// </summary>
    /// <remarks>
    /// 来自 <c>XiHan:Persistence:SqlSugar:Replicas</c>
    /// </remarks>
    [ApiLevel(Stability.Experimental, "5.0")]
    public SqlSugarReplicaOptions Replicas
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>InitTables</c>、<c>HealthProbeIntervalSeconds</c>、<c>CommandTimeoutSeconds</c>、<c>Connections:&lt;name&gt;</c>，以及 <c>Replicas</c> 下的 <c>DefaultHitRate</c>、<c>HealthCheck</c>、<c>HealthCheckIntervalSeconds</c>、<c>FailureCooldownSeconds</c>
    /// </remarks>
    /// <remarks>
    /// 值不合法抛出 <see cref="ArgumentException"/>（消息带配置键，不带连接串），不按缺省处理
    /// </remarks>
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

        // 0 不是「不覆盖」：ADO.NET 的 0 是无限等待，写错了也不能悄悄按没配处理
        var commandTimeout = configuration[$"{ConfigurationSection}:CommandTimeoutSeconds"];
        if (commandTimeout is not null)
        {
            options = int.TryParse(commandTimeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { CommandTimeout = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"'{ConfigurationSection}:CommandTimeoutSeconds' must be a positive integer, but was '{commandTimeout}'.");
        }

        var named = configuration.GetSection($"{ConfigurationSection}:Connections").GetChildren().ToList();
        if (named.Count > 0)
        {
            var connections = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var section in named)
            {
                // 子节（嵌套的键）不是连接串：按配置错误报出，不当作空值跳过
                connections[section.Key] = section.Value ?? throw new ArgumentException($"'{ConfigurationSection}:Connections:{section.Key}' must be a connection string.");
            }

            options = options with { Connections = connections };
        }

        return options with { Replicas = ReadReplicas(configuration) };
    }

    /// <summary>
    /// 读从库的权重与健康探测选项
    /// </summary>
    private static SqlSugarReplicaOptions ReadReplicas(IConfiguration configuration)
    {
        var replicas = new SqlSugarReplicaOptions();
        var section = $"{ConfigurationSection}:Replicas";
        if (configuration[$"{section}:DefaultHitRate"] is { } hitRate)
        {
            replicas = int.TryParse(hitRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? replicas with { DefaultHitRate = value }
                : throw new ArgumentException($"'{section}:DefaultHitRate' must be a positive integer, but was '{hitRate}'.");
        }

        if (configuration[$"{section}:HealthCheck"] is { } healthCheck)
        {
            replicas = bool.TryParse(healthCheck, out var value)
                ? replicas with { HealthCheck = value }
                : throw new ArgumentException($"'{section}:HealthCheck' must be 'true' or 'false', but was '{healthCheck}'.");
        }

        if (configuration[$"{section}:HealthCheckIntervalSeconds"] is { } interval)
        {
            replicas = double.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? replicas with { HealthCheckInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"'{section}:HealthCheckIntervalSeconds' must be a positive number, but was '{interval}'.");
        }

        if (configuration[$"{section}:FailureCooldownSeconds"] is { } cooldown)
        {
            replicas = double.TryParse(cooldown, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? replicas with { FailureCooldown = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"'{section}:FailureCooldownSeconds' must not be negative, but was '{cooldown}'.");
        }

        return replicas;
    }

    /// <summary>
    /// 核对命名连接
    /// </summary>
    /// <remarks>
    /// 名字不合文法、与约定连接同名、连接串空白都抛出 <see cref="ArgumentException"/>；消息只带名字。返回一份副本
    /// </remarks>
    private static Dictionary<string, string> ValidateConnections(IReadOnlyDictionary<string, string> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, connectionString) in connections)
        {
            // default 与 shard 是特性自己建的约定连接，命名连接与它们同名会顶替其一
            if (!DataSourceDefinition.IsValidName(name) || string.Equals(name, SqlSugarConnections.Shard, StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{ConfigurationSection}:Connections:{name}' is not a valid connection name: it must match '^[a-z0-9][a-z0-9-]{{0,63}}$' and must not be '{SqlSugarConnections.Default}' or '{SqlSugarConnections.Shard}'.", nameof(connections));
            }

            copy[name] = !string.IsNullOrWhiteSpace(connectionString)
                ? connectionString
                : throw new ArgumentException($"'{ConfigurationSection}:Connections:{name}' must not be blank.", nameof(connections));
        }

        return copy;
    }
}
