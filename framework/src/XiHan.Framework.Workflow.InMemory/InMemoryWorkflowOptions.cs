// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 内存工作流选项（配置节 <c>XiHan:Workflow:InMemory</c>）：各存储容量、轮询周期与批大小
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryWorkflowOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Workflow:InMemory";

    /// <summary>
    /// 定义存储容量
    /// </summary>
    /// <remarks>
    /// 默认 1000
    /// </remarks>
    public int MaxDefinitions
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be at least 1.");
    } = 1000;

    /// <summary>
    /// 实例存储容量
    /// </summary>
    /// <remarks>
    /// 默认 10000
    /// </remarks>
    public int MaxInstances
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be at least 1.");
    } = 10_000;

    /// <summary>
    /// 节点实例存储容量
    /// </summary>
    /// <remarks>
    /// 默认 100000
    /// </remarks>
    public int MaxNodeInstances
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be at least 1.");
    } = 100_000;

    /// <summary>
    /// 书签存储容量
    /// </summary>
    /// <remarks>
    /// 默认 100000
    /// </remarks>
    public int MaxBookmarks
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be at least 1.");
    } = 100_000;

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
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>MaxDefinitions</c>、<c>MaxInstances</c>、<c>MaxNodeInstances</c>、<c>MaxBookmarks</c>、<c>PollIntervalSeconds</c>、<c>BatchSize</c>。值不合法抛出 <see cref="ArgumentException"/>（消息带配置键与原值），不按缺省处理
    /// </remarks>
    public static InMemoryWorkflowOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new InMemoryWorkflowOptions();
        if (ConfigurationValues.ReadInt32(section, "MaxDefinitions", 1) is { } definitions)
        {
            options = options with { MaxDefinitions = definitions };
        }

        if (ConfigurationValues.ReadInt32(section, "MaxInstances", 1) is { } instances)
        {
            options = options with { MaxInstances = instances };
        }

        if (ConfigurationValues.ReadInt32(section, "MaxNodeInstances", 1) is { } nodes)
        {
            options = options with { MaxNodeInstances = nodes };
        }

        if (ConfigurationValues.ReadInt32(section, "MaxBookmarks", 1) is { } bookmarks)
        {
            options = options with { MaxBookmarks = bookmarks };
        }

        if (ConfigurationValues.ReadSeconds(section, "PollIntervalSeconds") is { } poll)
        {
            options = options with { PollInterval = poll };
        }

        if (ConfigurationValues.ReadInt32(section, "BatchSize", 1) is { } batch)
        {
            options = options with { BatchSize = batch };
        }

        return options;
    }
}
