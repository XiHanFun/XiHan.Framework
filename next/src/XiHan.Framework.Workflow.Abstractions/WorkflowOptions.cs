// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 引擎选项（配置节 <c>XiHan:Workflow</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Workflow";

    /// <summary>
    /// 一个波次最多执行的节点数；超过即实例故障（防死循环）。默认 1000。
    /// </summary>
    public int MaxNodeExecutionsPerBurst
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The burst limit must be at least 1.");
    } = 1000;

    /// <summary>
    /// 子流程最大深度；默认 16。
    /// </summary>
    public int MaxSubWorkflowDepth
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The depth must not be negative.");
    } = 16;

    /// <summary>
    /// 等实例锁的上限；默认 10 秒。
    /// </summary>
    public TimeSpan LockTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lock timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 到期书签因实例不在运行中而不能恢复时，把到期时间后推多久；默认 5 分钟。
    /// </summary>
    public TimeSpan NotResumableBackoff
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The backoff must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 表达式缓存上限；默认 10000。
    /// </summary>
    public int ExpressionCacheSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache size must be at least 1.");
    } = 10_000;

    /// <summary>
    /// 从配置读取：<c>MaxNodeExecutionsPerBurst</c>、<c>MaxSubWorkflowDepth</c>、<c>LockTimeoutSeconds</c>、<c>NotResumableBackoffSeconds</c>、<c>ExpressionCacheSize</c>。
    /// </summary>
    public static WorkflowOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new WorkflowOptions();
        if (int.TryParse(configuration[$"{ConfigurationSection}:MaxNodeExecutionsPerBurst"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var burst))
        {
            options = options with { MaxNodeExecutionsPerBurst = burst };
        }

        if (int.TryParse(configuration[$"{ConfigurationSection}:MaxSubWorkflowDepth"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth))
        {
            options = options with { MaxSubWorkflowDepth = depth };
        }

        if (double.TryParse(configuration[$"{ConfigurationSection}:LockTimeoutSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lockSeconds))
        {
            options = options with { LockTimeout = TimeSpan.FromSeconds(lockSeconds) };
        }

        if (double.TryParse(configuration[$"{ConfigurationSection}:NotResumableBackoffSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var backoff))
        {
            options = options with { NotResumableBackoff = TimeSpan.FromSeconds(backoff) };
        }

        if (int.TryParse(configuration[$"{ConfigurationSection}:ExpressionCacheSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cache))
        {
            options = options with { ExpressionCacheSize = cache };
        }

        return options;
    }
}
