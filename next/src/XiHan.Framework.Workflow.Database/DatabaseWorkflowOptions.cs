// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database;

/// <summary>
/// 数据库工作流选项（配置节 <c>XiHan:Workflow:Database</c>）。
/// </summary>
/// <remarks>
/// 到期书签的轮询周期与批大小、租约时长、本节点的持有者标识。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseWorkflowOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Workflow:Database";

    /// <summary>
    /// 本进程默认的持有者标识。
    /// </summary>
    /// <remarks>
    /// 机器名、进程 ID 与 8 位随机十六进制；进程内所有选项实例共用，同一进程的锁与领导权由它识别。
    /// </remarks>
    private static readonly string _defaultHolderId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// 到期书签的轮询周期。
    /// </summary>
    /// <remarks>
    /// 默认 1 秒。
    /// </remarks>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每次轮询最多恢复的书签数。
    /// </summary>
    /// <remarks>
    /// 默认 100。
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size must be at least 1.");
    } = 100;

    /// <summary>
    /// 实例锁与轮询领导权的租约时长。
    /// </summary>
    /// <remarks>
    /// 持有者每三分之一租约续约一次，持有者消失后别的节点在租约到期后接管；默认 60 秒。
    /// </remarks>
    public TimeSpan LockLease
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lock lease must be positive.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 本节点的持有者标识，写进租约行的 <c>Holder</c>。
    /// </summary>
    /// <remarks>
    /// 默认 <c>机器名:进程 ID:8 位随机十六进制</c>。
    /// </remarks>
    public string HolderId
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The holder ID must not be empty.", nameof(value));
    } = _defaultHolderId;

    /// <summary>
    /// 健康项 <c>workflow.database.poller</c> 的重探间隔（也是单次探测的超时）。
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
    /// 从配置读取。
    /// </summary>
    /// <remarks>
    /// <c>PollIntervalSeconds</c>、<c>BatchSize</c>、<c>LockLeaseSeconds</c>、<c>HolderId</c>、<c>HealthProbeIntervalSeconds</c>。
    /// </remarks>
    public static DatabaseWorkflowOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseWorkflowOptions();
        if (double.TryParse(configuration[$"{ConfigurationSection}:PollIntervalSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            options = options with { PollInterval = TimeSpan.FromSeconds(seconds) };
        }

        if (int.TryParse(configuration[$"{ConfigurationSection}:BatchSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var batch))
        {
            options = options with { BatchSize = batch };
        }

        if (double.TryParse(configuration[$"{ConfigurationSection}:LockLeaseSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lease))
        {
            options = options with { LockLease = TimeSpan.FromSeconds(lease) };
        }

        if (configuration[$"{ConfigurationSection}:HolderId"] is { } holder && !string.IsNullOrWhiteSpace(holder))
        {
            options = options with { HolderId = holder };
        }

        if (double.TryParse(configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var probe))
        {
            options = options with { HealthProbeInterval = TimeSpan.FromSeconds(probe) };
        }

        return options;
    }
}
