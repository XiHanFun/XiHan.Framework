// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.InMemory;

/// <summary>
/// 内存审计选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Auditing:InMemory</c> 显式读取：<c>Capacity</c>、<c>LogEvents</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryAuditingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Auditing:InMemory";

    /// <summary>
    /// 保留的事件数上限，满了丢最旧的。
    /// </summary>
    /// <remarks>
    /// 默认 10000。
    /// </remarks>
    public int Capacity
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be positive.");
    } = 10_000;

    /// <summary>
    /// 是否把每条事件写进日志（Information 级）。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="true"/>。
    /// </remarks>
    public bool LogEvents { get; init; } = true;

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static InMemoryAuditingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryAuditingOptions();
        if (configuration[$"{ConfigurationSection}:Capacity"] is { } capacity)
        {
            options = int.TryParse(capacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? options with { Capacity = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Capacity must be a positive integer, but was '{capacity}'.");
        }

        if (configuration[$"{ConfigurationSection}:LogEvents"] is { } log)
        {
            options = bool.TryParse(log, out var parsed)
                ? options with { LogEvents = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:LogEvents must be 'true' or 'false', but was '{log}'.");
        }

        return options;
    }
}
