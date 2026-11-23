// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// VectorData 知识库 Provider 自己的选项（配置节 <c>XiHan:AI:VectorData</c>）
/// </summary>
/// <remarks>
/// 集合、维度、切块与检索条数等知识库选项仍在 <c>XiHan:AI:Knowledge</c>，向量存储的连接由应用登记的连接器决定
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record VectorDataKnowledgeOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:AI:VectorData";

    /// <summary>
    /// 健康项 <c>ai.vectordata.store</c> 的重探间隔（也是单次探测的超时）
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
    /// 从配置读取：<c>HealthProbeIntervalSeconds</c>
    /// </summary>
    /// <remarks>
    /// 值不是有限的正数或超出 <see cref="TimeSpan"/> 的范围时抛出 <see cref="ArgumentException"/>（启动失败），不按缺省处理
    /// </remarks>
    public static VectorDataKnowledgeOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new VectorDataKnowledgeOptions();
        return ConfigurationValues.ReadSeconds(configuration.GetSection(ConfigurationSection), "HealthProbeIntervalSeconds") is { } interval
            ? options with { HealthProbeInterval = interval }
            : options;
    }
}
