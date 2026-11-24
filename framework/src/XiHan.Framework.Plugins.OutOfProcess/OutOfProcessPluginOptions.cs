// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件宿主选项（配置节 <c>XiHan:Plugins:OutOfProcess</c>）
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OutOfProcessPluginOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Plugins:OutOfProcess";

    /// <summary>
    /// 启动时装载的插件目录（每个目录一个插件，含 <c>xihan.manifest.json</c>）
    /// </summary>
    /// <remarks>
    /// 相对路径相对当前目录
    /// </remarks>
    public IReadOnlyList<string> Directories { get; init; } = [];

    /// <summary>
    /// 按清单 ID 授予的权限（配置 <c>Grants:&lt;清单 ID&gt;</c>）
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Grants { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// 读到握手行并完成 <c>Handshake</c> 的时限
    /// </summary>
    /// <remarks>
    /// 默认 10 秒
    /// </remarks>
    public TimeSpan HandshakeTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The handshake timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// <c>Configure</c> 与 <c>Start</c> 各自的时限
    /// </summary>
    /// <remarks>
    /// 默认 30 秒
    /// </remarks>
    public TimeSpan StartTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The start timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 停止宽限
    /// </summary>
    /// <remarks>
    /// 为空时取内核的 <c>XiHan:Kernel:StopTimeoutSeconds</c>
    /// </remarks>
    public TimeSpan? StopGrace
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The stop grace must be positive.");
    }

    /// <summary>
    /// 健康推送不可用时的轮询间隔
    /// </summary>
    /// <remarks>
    /// 默认 10 秒
    /// </remarks>
    public TimeSpan HealthInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health interval must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 连续多少次健康探测失败视为进程失效
    /// </summary>
    /// <remarks>
    /// 默认 3
    /// </remarks>
    public int HealthFailures
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health failure threshold must be at least 1.");
    } = 3;

    /// <summary>
    /// <see cref="RestartWindow"/> 内最多自动重启多少次，之后保持 Failed
    /// </summary>
    /// <remarks>
    /// 默认 5
    /// </remarks>
    public int RestartLimit
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The restart limit must not be negative.");
    } = 5;

    /// <summary>
    /// 统计重启次数的时间窗
    /// </summary>
    /// <remarks>
    /// 默认 10 分钟
    /// </remarks>
    public TimeSpan RestartWindow
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The restart window must be positive.");
    } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 插件工作目录的根（每个插件一个子目录）
    /// </summary>
    /// <remarks>
    /// 为空时取临时目录下的 <c>xihan-plugins</c>
    /// </remarks>
    public string? WorkDirectory { get; init; }

    /// <summary>
    /// 清单 ID 对应的授予
    /// </summary>
    /// <remarks>
    /// 未配置为空授予
    /// </remarks>
    public PluginPermissions GrantsFor(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return Grants.TryGetValue(manifestId, out var granted) ? new PluginPermissions(granted) : PluginPermissions.Empty;
    }

    /// <summary>
    /// 工作目录根的绝对路径
    /// </summary>
    /// <remarks>
    /// 没有配置时是临时目录下按用户分开的 <c>xihan-plugins-&lt;用户名&gt;</c>，Unix 上必须是私有目录（ADR-0207）
    /// </remarks>
    public string ResolveWorkDirectory()
        => WorkDirectory is { } configured ? Path.GetFullPath(configured) : PluginWorkDirectories.DefaultRoot("xihan-plugins");

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 各时长以秒为单位（<c>HandshakeTimeoutSeconds</c>、<c>StartTimeoutSeconds</c>、<c>StopGraceSeconds</c>、<c>HealthIntervalSeconds</c>、<c>RestartWindowSeconds</c>），是正数；<c>HealthFailures</c> 是正整数，<c>RestartLimit</c> 是非负整数。不合规的值以带配置键的 <see cref="ArgumentException"/> 拒绝
    /// </remarks>
    public static OutOfProcessPluginOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new OutOfProcessPluginOptions
        {
            Directories = section.GetSection("Directories").GetChildren().Select(static c => c.Value).OfType<string>().Where(static d => !string.IsNullOrWhiteSpace(d)).ToList(),
            Grants = section.GetSection("Grants").GetChildren().ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<string>)g.GetChildren().Select(static c => c.Value).OfType<string>().ToList(),
                StringComparer.Ordinal),
            WorkDirectory = section["WorkDirectory"],
        };

        if (ConfigurationValues.ReadSeconds(section,"HandshakeTimeoutSeconds") is { } handshake)
        {
            options = options with { HandshakeTimeout = handshake };
        }

        if (ConfigurationValues.ReadSeconds(section,"StartTimeoutSeconds") is { } start)
        {
            options = options with { StartTimeout = start };
        }

        if (ConfigurationValues.ReadSeconds(section,"StopGraceSeconds") is { } grace)
        {
            options = options with { StopGrace = grace };
        }

        if (ConfigurationValues.ReadSeconds(section,"HealthIntervalSeconds") is { } health)
        {
            options = options with { HealthInterval = health };
        }

        if (ConfigurationValues.ReadSeconds(section,"RestartWindowSeconds") is { } window)
        {
            options = options with { RestartWindow = window };
        }

        if (ConfigurationValues.ReadInt32(section, "HealthFailures", 1) is { } failures)
        {
            options = options with { HealthFailures = failures };
        }

        if (ConfigurationValues.ReadInt32(section, "RestartLimit", 0) is { } limit)
        {
            options = options with { RestartLimit = limit };
        }

        return options;
    }
}
