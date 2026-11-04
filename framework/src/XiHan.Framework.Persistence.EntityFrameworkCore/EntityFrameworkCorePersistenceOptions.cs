// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core Provider 选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:EntityFrameworkCore</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record EntityFrameworkCorePersistenceOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:EntityFrameworkCore";

    /// <summary>
    /// 保留的连接名：带租户分片的调用里的连接
    /// </summary>
    private const string ShardName = "shard";

    /// <summary>
    /// 启动时调用 <c>EnsureCreated</c> 建表
    /// </summary>
    /// <remarks>
    /// 只用于开发与测试；生产用迁移。默认 <see langword="false"/>
    /// </remarks>
    public bool EnsureCreated { get; init; }

    /// <summary>
    /// 健康项 <c>persistence.entityframeworkcore.database</c> 的重探间隔
    /// </summary>
    /// <remarks>
    /// 也是单次探测的超时。默认 15 秒
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 核对通过的租户分片库最多记多少个
    /// </summary>
    /// <remarks>
    /// SQLite 上租户分片库首次绑定时核对时间列（ADR-0097），核对通过的按分片连接串记在进程内、之后不再查；记满时淘汰最早记下的，被淘汰的分片下次绑定时重新核对一次。默认 1024，至少 1
    /// </remarks>
    public int VerifiedShardCapacity
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The verified shard capacity must be positive.");
    } = 1024;

    /// <summary>
    /// 命名连接：名字 → 连接串
    /// </summary>
    /// <remarks>
    /// 来自 <c>XiHan:Persistence:EntityFrameworkCore:Connections:&lt;name&gt;</c>，缺省为空。供登记到命名连接的实体与绑定连接的工作单元使用（RFC-0005「命名连接」）：绑定这个连接的工作单元所在作用域的 DbContext 按它建出——配置回调收到的绑定主值是这个连接串、属性沿用 <c>db</c> 绑定。名字合 <c>^[a-z0-9][a-z0-9-]{0,63}$</c>，不能是 <c>default</c> 或 <c>shard</c>（与 SqlSugar 的约定连接同名规则一致）；连接串是机密，不进日志
    /// </remarks>
    public IReadOnlyDictionary<string, string> Connections
    {
        get => field;
        init => field = ValidateConnections(value);
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 读 <c>EnsureCreated</c>、<c>HealthProbeIntervalSeconds</c>、<c>VerifiedShardCapacity</c>、<c>Connections:&lt;name&gt;</c>。值不合法抛出 <see cref="ArgumentException"/>（消息带配置键，不带连接串），不按缺省处理
    /// </remarks>
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

        var capacity = configuration[$"{ConfigurationSection}:VerifiedShardCapacity"];
        if (capacity is not null)
        {
            // NumberStyles.None 只接受纯数字：带符号、空白或千分位的值一律视为非法，而不是宽松解析
            options = int.TryParse(capacity, NumberStyles.None, CultureInfo.InvariantCulture, out var verified) && verified >= 1
                ? options with { VerifiedShardCapacity = verified }
                : throw new ArgumentException($"'{ConfigurationSection}:VerifiedShardCapacity' must be a positive integer, but was '{capacity}'.");
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

        return options;
    }

    /// <summary>
    /// 核对命名连接
    /// </summary>
    /// <remarks>
    /// 名字不合文法、是 <c>default</c> 或 <c>shard</c>、连接串空白都抛出 <see cref="ArgumentException"/>；消息只带名字。返回一份副本
    /// </remarks>
    private static Dictionary<string, string> ValidateConnections(IReadOnlyDictionary<string, string> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, connectionString) in connections)
        {
            // default 是缺省落点、shard 是带租户分片的调用里的连接：与 SqlSugar 一样保留，换 Provider 时同一份配置照样合法
            if (!DataSourceDefinition.IsValidName(name) || string.Equals(name, ShardName, StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{ConfigurationSection}:Connections:{name}' is not a valid connection name: it must match '^[a-z0-9][a-z0-9-]{{0,63}}$' and must not be 'default' or '{ShardName}'.", nameof(connections));
            }

            copy[name] = !string.IsNullOrWhiteSpace(connectionString)
                ? connectionString
                : throw new ArgumentException($"'{ConfigurationSection}:Connections:{name}' must not be blank.", nameof(connections));
        }

        return copy;
    }
}
