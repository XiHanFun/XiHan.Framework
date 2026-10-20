// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性启动失败时宿主的处理策略。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum FailurePolicy
{
    /// <summary>终止启动，抛出 <see cref="KernelErrors.FeatureStartFailed"/>。</summary>
    TerminateStartup = 0,

    /// <summary>隔离该特性（<see cref="LifecycleState.Failed"/>），继续启动其余特性；应用不就绪。</summary>
    IsolateFeature = 1,
}

/// <summary>
/// 内核选项。从配置节 <c>XiHan:Kernel</c> 显式读取，不经反射绑定。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record KernelOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Kernel";

    /// <summary>
    /// 单个特性的启动超时。
    /// </summary>
    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 单个特性的停止超时。
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 启动失败策略。
    /// </summary>
    public FailurePolicy FailurePolicy { get; init; } = FailurePolicy.TerminateStartup;

    /// <summary>
    /// 是否自动加入内核的内置中间件（Observe、Resilience 阶段）。
    /// </summary>
    public bool UseBuiltInMiddleware { get; init; } = true;

    /// <summary>
    /// 从配置读取；缺省键保持默认值，非法值抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static KernelOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new KernelOptions();

        return options with
        {
            StartTimeout = ReadSeconds(section, "StartTimeoutSeconds") ?? options.StartTimeout,
            StopTimeout = ReadSeconds(section, "StopTimeoutSeconds") ?? options.StopTimeout,
            FailurePolicy = ReadEnum<FailurePolicy>(section, "FailurePolicy") ?? options.FailurePolicy,
            UseBuiltInMiddleware = ReadBool(section, "UseBuiltInMiddleware") ?? options.UseBuiltInMiddleware,
        };
    }

    private static TimeSpan? ReadSeconds(IConfiguration section, string key)
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{key} 必须是正数，实际 '{raw}'。");
    }

    private static bool? ReadBool(IConfiguration section, string key)
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return bool.TryParse(raw, out var value) ? value : throw new ArgumentException($"{ConfigurationSection}:{key} 必须是 true 或 false，实际 '{raw}'。");
    }

    private static TEnum? ReadEnum<TEnum>(IConfiguration section, string key)
        where TEnum : struct, Enum
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{key} 的值 '{raw}' 不是 {typeof(TEnum).Name} 的合法取值。");
    }
}
