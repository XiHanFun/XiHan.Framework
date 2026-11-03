// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
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
    /// 健康项 <c>persistence.datasource.&lt;name&gt;</c> 的重探间隔（也是单次探测的超时）
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
        var options = new DataSourceOptions();
        var count = configuration[$"{ConfigurationSection}:MaxCount"];
        if (count is not null)
        {
            options = int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= MaxCountLimit
                ? options with { MaxCount = value }
                : throw new ArgumentException($"'{ConfigurationSection}:MaxCount' must be an integer between 1 and {MaxCountLimit}, but was '{count}'.");
        }

        var probe = configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"];
        if (probe is not null)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && !double.IsInfinity(seconds)
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"'{ConfigurationSection}:HealthProbeIntervalSeconds' must be a positive number, but was '{probe}'.");
        }

        return options;
    }
}
