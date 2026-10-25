// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 数据库本地化选项。从配置节 <c>XiHan:Localization:Database</c> 显式读取：<c>RefreshIntervalSeconds</c>（0 关闭周期刷新）、<c>MaxTranslations</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseLocalizationOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Localization:Database";

    /// <summary>
    /// 周期刷新的间隔；默认 5 分钟，<see langword="null"/> 只在启动时加载一次（之后靠 <see cref="MessageTranslationLoader.ReloadAsync"/>）。
    /// </summary>
    public TimeSpan? RefreshInterval
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The refresh interval must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 加载的翻译条数上限（有界快照）；默认 100000，超出的部分不加载并记警告。
    /// </summary>
    public int MaxTranslations
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of translations must be positive.");
    } = 100_000;

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static DatabaseLocalizationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseLocalizationOptions();
        if (configuration[$"{ConfigurationSection}:RefreshIntervalSeconds"] is { } refresh)
        {
            options = double.TryParse(refresh, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? options with { RefreshInterval = seconds == 0 ? null : TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:RefreshIntervalSeconds must be a non-negative number, but was '{refresh}'.");
        }

        if (configuration[$"{ConfigurationSection}:MaxTranslations"] is { } max)
        {
            options = int.TryParse(max, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? options with { MaxTranslations = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:MaxTranslations must be a positive integer, but was '{max}'.");
        }

        return options;
    }
}
