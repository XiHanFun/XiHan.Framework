// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存持久化选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:InMemory</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InMemoryPersistenceOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:InMemory";

    /// <summary>
    /// 每种实体的容量上限
    /// </summary>
    public int MaxEntitiesPerType
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The capacity must be positive.");
    } = 100_000;

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static InMemoryPersistenceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ConfigurationValues.ReadInt32(configuration.GetSection(ConfigurationSection), "MaxEntitiesPerType", 1) is { } capacity
            ? new InMemoryPersistenceOptions { MaxEntitiesPerType = capacity }
            : new InMemoryPersistenceOptions();
    }
}
