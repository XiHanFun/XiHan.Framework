// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 内存存储的选项（配置节 <c>XiHan:Scheduling:InMemory</c>）；引擎选项在 <c>XiHan:Scheduling</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemorySchedulingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Scheduling:InMemory";

    /// <summary>
    /// 运行历史上限；超出时丢弃最旧的已结束记录。默认 1000。
    /// </summary>
    public int MaxRuns
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of runs must be at least 1.");
    } = 1000;

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static InMemorySchedulingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemorySchedulingOptions();
        var raw = configuration[$"{ConfigurationSection}:MaxRuns"];
        if (raw is not null)
        {
            options = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) && max >= 1
                ? options with { MaxRuns = max }
                : throw new ArgumentException($"{ConfigurationSection}:MaxRuns must be a positive integer, but was '{raw}'.");
        }

        return options;
    }
}
