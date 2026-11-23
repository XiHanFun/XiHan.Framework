// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 运行信息的选项
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Observability:Host</c>：<c>SampleIntervalSeconds</c>（采样间隔，默认 5 秒，取值 (0, 3600]）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record HostObservabilityOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Observability:Host";

    /// <summary>
    /// 采样间隔上限
    /// </summary>
    public static readonly TimeSpan MaxSampleInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// 采样间隔
    /// </summary>
    /// <remarks>
    /// CPU 使用率与网卡速率是相邻两次采样之差
    /// </remarks>
    public TimeSpan SampleInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero && value <= MaxSampleInterval ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The sample interval must be positive and at most one hour.");
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static HostObservabilityOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new HostObservabilityOptions();
        var section = configuration.GetSection(ConfigurationSection);
        if (ConfigurationValues.ReadSeconds(section, "SampleIntervalSeconds") is { } interval)
        {
            // 上界统一读取表达不了，留在这里判：留给属性去拒绝的话，消息里没有配置键
            options = interval <= MaxSampleInterval
                ? options with { SampleInterval = interval }
                : throw new ArgumentException($"'{ConfigurationSection}:SampleIntervalSeconds' must be a number of seconds in (0, 3600], but was '{section["SampleIntervalSeconds"]}'.");
        }

        return options;
    }
}
