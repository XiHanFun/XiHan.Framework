// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 数据库工作流选项（配置节 <c>XiHan:Workflow:Database</c>）
/// </summary>
/// <remarks>
/// 到期书签的轮询周期与批大小、租约时长、本节点的持有者标识、租约表的命名连接
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseWorkflowOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Workflow:Database";

    /// <summary>
    /// 本进程默认的持有者标识
    /// </summary>
    /// <remarks>
    /// 机器名、进程 ID 与 8 位随机十六进制；进程内所有选项实例共用，同一进程的锁与领导权由它识别
    /// </remarks>
    private static readonly string _defaultHolderId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// 到期书签的轮询周期
    /// </summary>
    /// <remarks>
    /// 默认 1 秒
    /// </remarks>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每次轮询最多恢复的书签数
    /// </summary>
    /// <remarks>
    /// 默认 100
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size must be at least 1.");
    } = 100;

    /// <summary>
    /// 实例锁与轮询领导权的租约时长
    /// </summary>
    /// <remarks>
    /// 持有者每三分之一租约续约一次，持有者消失后别的节点在租约到期后接管；默认 60 秒
    /// </remarks>
    public TimeSpan LockLease
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lock lease must be positive.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 本节点的持有者标识，写进租约行的 <c>Holder</c>
    /// </summary>
    /// <remarks>
    /// 默认 <c>机器名:进程 ID:8 位随机十六进制</c>。不能为空白，也不能有孤立的代理项（ADR-0334）：它是租约行 <c>Holder</c> 的前缀，经数据库参数宽松编码之后存下的是另一段文本，两个只差在孤立代理项上的节点在租约表里分不开
    /// </remarks>
    public string HolderId
    {
        get => field;
        init => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The holder ID must not be empty.", nameof(value))
            : !UnicodeText.HasLoneSurrogate(value) ? value : throw new ArgumentException("The holder ID must not contain lone surrogates.", nameof(value));
    } = _defaultHolderId;

    /// <summary>
    /// 租约表（<c>WorkflowLockRecord</c>）的命名连接
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时租约表在持久化的缺省落点（平台库）。非空时特性把租约表登记到这个连接，租约的每一步与健康探针开绑定这个连接的工作单元，事务只开在租约库上（RFC-0005「命名连接」）。SQLite 上必须配：SQLite 只有一个写者，引擎在调用方事务里写过之后再写租约行，同一个库文件上要等的正是调用方自己的事务，忙等超时后以 <c>WORKFLOW_TIMEOUT_001</c> 失败；PostgreSQL 等行级锁的数据库不需要。SqlSugar 是客户端里同名 <c>ConfigId</c> 的连接（方式一在 <c>XiHan:Persistence:SqlSugar:Connections</c> 声明，不在客户端里启动即失败 <c>PERSISTENCE_VALIDATION_004</c>）；EF Core 在 <c>XiHan:Persistence:EntityFrameworkCore:Connections</c> 声明（ADR-0087：租约的每一步在绑定这个连接的工作单元里，作用域里的 <c>DbContext</c> 按租约库建出；没有声明装配即失败 <c>PERSISTENCE_VALIDATION_004</c>）；内存持久化没有连接，配了即装配失败（<c>PERSISTENCE_UNAVAILABLE_008</c>）。不能是空白
    /// </remarks>
    public string? LockConnection
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The lock connection name must not be blank.", nameof(value));
    }

    /// <summary>
    /// 健康项 <c>workflow.database.poller</c> 的重探间隔（也是单次探测的超时）
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
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>PollIntervalSeconds</c>、<c>BatchSize</c>、<c>LockLeaseSeconds</c>、<c>HolderId</c>、<c>LockConnection</c>、<c>HealthProbeIntervalSeconds</c>。值不合法抛出 <see cref="ArgumentException"/>（消息带配置键与原值），不按缺省处理；<c>HolderId</c> 与 <c>LockConnection</c> 空白视为未配置，与调度选项的 <c>NodeId</c> 一致
    /// </remarks>
    public static DatabaseWorkflowOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new DatabaseWorkflowOptions();
        if (ConfigurationValues.ReadSeconds(section, "PollIntervalSeconds") is { } poll)
        {
            options = options with { PollInterval = poll };
        }

        if (ConfigurationValues.ReadInt32(section, "BatchSize", 1) is { } batch)
        {
            options = options with { BatchSize = batch };
        }

        if (ConfigurationValues.ReadSeconds(section, "LockLeaseSeconds") is { } lease)
        {
            options = options with { LockLease = lease };
        }

        if (configuration[$"{ConfigurationSection}:HolderId"] is { } holder && !string.IsNullOrWhiteSpace(holder))
        {
            options = options with { HolderId = holder };
        }

        if (configuration[$"{ConfigurationSection}:LockConnection"] is { } connection && !string.IsNullOrWhiteSpace(connection))
        {
            options = options with { LockConnection = connection };
        }

        if (ConfigurationValues.ReadSeconds(section, "HealthProbeIntervalSeconds") is { } probe)
        {
            options = options with { HealthProbeInterval = probe };
        }

        return options;
    }
}
