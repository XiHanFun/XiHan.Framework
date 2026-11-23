// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database;

/// <summary>
/// 数据库审计选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Auditing:Database</c> 显式读取：<c>QueueCapacity</c>、<c>BatchSize</c>、<c>FlushIntervalSeconds</c>、<c>LogEvents</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseAuditingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Auditing:Database";

    /// <summary>
    /// 等待落库的事件数上限
    /// </summary>
    /// <remarks>
    /// 队列满了新到的事件被丢弃并计数；默认 10000
    /// </remarks>
    public int QueueCapacity
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The queue capacity must be positive.");
    } = 10_000;

    /// <summary>
    /// 一批落库的事件数上限
    /// </summary>
    /// <remarks>
    /// 攒够这么多就立即刷写；默认 100
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size must be positive.");
    } = 100;

    /// <summary>
    /// 刷写间隔
    /// </summary>
    /// <remarks>
    /// 不足一批也按此周期刷写；默认 1 秒
    /// </remarks>
    public TimeSpan FlushInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The flush interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 是否把每条事件写进日志（Information 级）
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="true"/>
    /// </remarks>
    public bool LogEvents { get; init; } = true;

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static DatabaseAuditingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseAuditingOptions();
        var section = configuration.GetSection(ConfigurationSection);
        if (ConfigurationValues.ReadInt32(section, "QueueCapacity", 1) is { } capacity)
        {
            options = options with { QueueCapacity = capacity };
        }

        if (ConfigurationValues.ReadInt32(section, "BatchSize", 1) is { } batch)
        {
            options = options with { BatchSize = batch };
        }

        if (ConfigurationValues.ReadSeconds(section, "FlushIntervalSeconds") is { } interval)
        {
            options = options with { FlushInterval = interval };
        }

        if (ConfigurationValues.ReadBoolean(section, "LogEvents") is { } log)
        {
            options = options with { LogEvents = log };
        }

        return options;
    }
}
