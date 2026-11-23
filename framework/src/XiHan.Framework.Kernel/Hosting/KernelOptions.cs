// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 内核选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Kernel</c> 显式读取，不经反射绑定
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record KernelOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Kernel";

    /// <summary>
    /// 单个特性的启动超时
    /// </summary>
    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 单个特性的停止超时
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 启动失败策略
    /// </summary>
    public FailurePolicy FailurePolicy { get; init; } = FailurePolicy.TerminateStartup;

    /// <summary>
    /// 是否自动加入内核的内置中间件（Observe、Resilience 阶段）
    /// </summary>
    public bool UseBuiltInMiddleware { get; init; } = true;

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 缺省键保持默认值，非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static KernelOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new KernelOptions();

        // 每个键单独读取并显式解析，缺省回落到默认实例的值；不用 Bind 以保持零反射与 AOT 友好
        return options with
        {
            StartTimeout = ConfigurationValues.ReadSeconds(section, "StartTimeoutSeconds") ?? options.StartTimeout,
            StopTimeout = ConfigurationValues.ReadSeconds(section, "StopTimeoutSeconds") ?? options.StopTimeout,
            FailurePolicy = ConfigurationValues.ReadEnum<FailurePolicy>(section, "FailurePolicy") ?? options.FailurePolicy,
            UseBuiltInMiddleware = ConfigurationValues.ReadBoolean(section, "UseBuiltInMiddleware") ?? options.UseBuiltInMiddleware,
        };
    }
}
