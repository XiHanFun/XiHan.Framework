// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 数据库本地化选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Localization:Database</c> 显式读取：<c>RefreshIntervalSeconds</c>（0 关闭周期刷新）、<c>ChangeCheckIntervalSeconds</c>（0 不核对版本戳）、<c>MaxTranslations</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DatabaseLocalizationOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Localization:Database";

    /// <summary>
    /// 周期刷新的间隔
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟，<see langword="null"/> 不做周期刷新。经 <see cref="MessageTranslationStore"/> 的写入靠版本戳立即生效（见 <see cref="ChangeCheckInterval"/>），周期刷新兜住绕过它直接改表与版本戳丢失
    /// </remarks>
    public TimeSpan? RefreshInterval
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The refresh interval must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 核对翻译版本戳的间隔
    /// </summary>
    /// <remarks>
    /// 默认 1 秒，<see langword="null"/> 不核对。<see cref="MessageTranslationStore"/> 写入提交后在 <c>ICache</c> 里换一个版本戳，每个实例按这个间隔读它（两级缓存下是本机一级缓存命中，不出进程），变了就重新加载，所有实例在一个间隔内用上新翻译（ADR-0229）
    /// </remarks>
    public TimeSpan? ChangeCheckInterval
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The change check interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 加载的翻译条数上限（有界快照）
    /// </summary>
    /// <remarks>
    /// 默认 100000，超出的部分不加载并记警告
    /// </remarks>
    public int MaxTranslations
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of translations must be positive.");
    } = 100_000;

    /// <summary>
    /// 从配置读取
    /// </summary>
    public static DatabaseLocalizationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new DatabaseLocalizationOptions();
        var section = configuration.GetSection(ConfigurationSection);
        // 两个间隔允许 0：0 表示关闭，对应属性的 null
        if (ConfigurationValues.ReadSeconds(section, "RefreshIntervalSeconds", allowZero: true) is { } refresh)
        {
            options = options with { RefreshInterval = refresh == TimeSpan.Zero ? null : refresh };
        }

        if (ConfigurationValues.ReadSeconds(section, "ChangeCheckIntervalSeconds", allowZero: true) is { } check)
        {
            options = options with { ChangeCheckInterval = check == TimeSpan.Zero ? null : check };
        }

        if (ConfigurationValues.ReadInt32(section, "MaxTranslations", 1) is { } max)
        {
            options = options with { MaxTranslations = max };
        }

        return options;
    }
}
