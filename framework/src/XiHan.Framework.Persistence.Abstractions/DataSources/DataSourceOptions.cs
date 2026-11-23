// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 运行时数据源注册表的选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:DataSources</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DataSourceOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:DataSources";

    /// <summary>
    /// 登记数的上限
    /// </summary>
    public const int MaxCountLimit = 1024;

    /// <summary>
    /// 最多登记多少个数据源
    /// </summary>
    /// <remarks>
    /// 1 到 <see cref="MaxCountLimit"/>，缺省 64。满了再登记新名字返回 <see cref="PersistenceErrors.DataSourceCapacityExceeded"/>，不淘汰已有的；替换同名的不占名额
    /// </remarks>
    public int MaxCount
    {
        get => field;
        init => field = value is >= 1 and <= MaxCountLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The data source capacity must be between 1 and {MaxCountLimit}.");
    } = 64;

    /// <summary>
    /// 健康项 <c>persistence.datasource.&lt;name&gt;</c> 的重探间隔（也是单次探测与测试连接的超时）
    /// </summary>
    /// <remarks>
    /// 缺省 15 秒
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置读取：<c>MaxCount</c>、<c>HealthProbeIntervalSeconds</c>
    /// </summary>
    /// <exception cref="ArgumentException">值不合法</exception>
    public static DataSourceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new DataSourceOptions();
        if (ConfigurationValues.ReadInt32(section, "MaxCount", 1, MaxCountLimit) is { } count)
        {
            options = options with { MaxCount = count };
        }

        if (ConfigurationValues.ReadSeconds(section, "HealthProbeIntervalSeconds") is { } probe)
        {
            options = options with { HealthProbeInterval = probe };
        }

        return options;
    }
}
