// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主选项（配置节 <c>XiHan:Plugins:Wasm</c>）。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed record WasmPluginOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Plugins:Wasm";

    /// <summary>
    /// 启动时装载的插件目录（每个目录一个插件，含 <c>xihan.manifest.json</c> 与模块文件）。
    /// </summary>
    /// <remarks>
    /// 相对路径相对当前目录。
    /// </remarks>
    public IReadOnlyList<string> Directories { get; init; } = [];

    /// <summary>
    /// 按清单 ID 授予的 <c>capability:</c> 权限（配置 <c>Grants:&lt;清单 ID&gt;</c>）。
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Grants { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// 按清单 ID 的文件系统映射。
    /// </summary>
    /// <remarks>
    /// 客体路径 → 宿主目录（配置 <c>Mounts:&lt;清单 ID&gt;:&lt;客体路径&gt;</c>）；清单的 <c>fs.read:</c> / <c>fs.write:</c> 权限只在映射了才算授予。
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Mounts { get; init; } = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

    /// <summary>
    /// 线性内存上限。
    /// </summary>
    /// <remarks>
    /// 默认 64 MiB。
    /// </remarks>
    public long MemoryLimitBytes
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The memory limit must be positive.");
    } = 64L * 1024 * 1024;

    /// <summary>
    /// Wasm 栈上限。
    /// </summary>
    /// <remarks>
    /// 默认 1 MiB。
    /// </remarks>
    public int StackLimitBytes
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The stack limit must be positive.");
    } = 1024 * 1024;

    /// <summary>
    /// epoch 递增间隔（CPU 截止时间的粒度）。
    /// </summary>
    /// <remarks>
    /// 默认 10 毫秒。
    /// </remarks>
    public TimeSpan EpochInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The epoch interval must be positive.");
    } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// 生命周期调用（握手、配置、启动、停止、健康）与没有截止时间的调用的时限。
    /// </summary>
    /// <remarks>
    /// 默认 30 秒。
    /// </remarks>
    public TimeSpan CallTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The call timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 中间件钩子每次调用的时限。
    /// </summary>
    /// <remarks>
    /// 默认 100 毫秒。
    /// </remarks>
    public TimeSpan MiddlewareTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The middleware timeout must be positive.");
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 健康轮询间隔。
    /// </summary>
    /// <remarks>
    /// 默认 10 秒。
    /// </remarks>
    public TimeSpan HealthInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health interval must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 连续多少次健康探测失败视为实例失效。
    /// </summary>
    /// <remarks>
    /// 默认 3。
    /// </remarks>
    public int HealthFailures
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health failure threshold must be at least 1.");
    } = 3;

    /// <summary>
    /// <see cref="RestartWindow"/> 内最多自动重建多少次，之后保持 Failed。
    /// </summary>
    /// <remarks>
    /// 默认 5。
    /// </remarks>
    public int RestartLimit
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The restart limit must not be negative.");
    } = 5;

    /// <summary>
    /// 统计重建次数的时间窗。
    /// </summary>
    /// <remarks>
    /// 默认 10 分钟。
    /// </remarks>
    public TimeSpan RestartWindow
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The restart window must be positive.");
    } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 插件工作目录的根（每个插件一个子目录，存放 WASI 的 stdout / stderr）。
    /// </summary>
    /// <remarks>
    /// 为空时取临时目录下的 <c>xihan-plugins-wasm</c>。
    /// </remarks>
    public string? WorkDirectory { get; init; }

    /// <summary>
    /// 清单 ID 对应的授予。
    /// </summary>
    /// <remarks>
    /// 未配置为空授予。
    /// </remarks>
    public PermissionGrant GrantsFor(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return Grants.TryGetValue(manifestId, out var granted) ? new PermissionGrant(granted) : PermissionGrant.Empty;
    }

    /// <summary>
    /// 清单 ID 对应的文件系统映射。
    /// </summary>
    /// <remarks>
    /// 未配置为空。
    /// </remarks>
    public IReadOnlyDictionary<string, string> MountsFor(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return Mounts.TryGetValue(manifestId, out var mounts) ? mounts : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 工作目录根的绝对路径。
    /// </summary>
    public string ResolveWorkDirectory()
        => Path.GetFullPath(WorkDirectory ?? Path.Combine(Path.GetTempPath(), "xihan-plugins-wasm"));

    /// <summary>
    /// 从配置读取。
    /// </summary>
    /// <remarks>
    /// 时长以秒为单位（<c>CallTimeoutSeconds</c>、<c>HealthIntervalSeconds</c>、<c>RestartWindowSeconds</c>）或毫秒为单位（<c>EpochIntervalMilliseconds</c>、<c>MiddlewareTimeoutMilliseconds</c>）。
    /// </remarks>
    public static WasmPluginOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new WasmPluginOptions
        {
            Directories = section.GetSection("Directories").GetChildren().Select(static c => c.Value).OfType<string>().Where(static d => !string.IsNullOrWhiteSpace(d)).ToList(),
            Grants = section.GetSection("Grants").GetChildren().ToDictionary(
                static g => g.Key,
                static g => (IReadOnlyList<string>)g.GetChildren().Select(static c => c.Value).OfType<string>().ToList(),
                StringComparer.Ordinal),
            Mounts = section.GetSection("Mounts").GetChildren().ToDictionary(
                static m => m.Key,
                static m => (IReadOnlyDictionary<string, string>)m.GetChildren().Where(static c => c.Value is not null).ToDictionary(static c => c.Key, static c => c.Value!, StringComparer.Ordinal),
                StringComparer.Ordinal),
            WorkDirectory = section["WorkDirectory"],
        };

        if (Number(section, "MemoryLimitBytes") is { } memory)
        {
            options = options with { MemoryLimitBytes = (long)memory };
        }

        if (Number(section, "StackLimitBytes") is { } stack)
        {
            options = options with { StackLimitBytes = (int)stack };
        }

        if (Number(section, "EpochIntervalMilliseconds") is { } epoch)
        {
            options = options with { EpochInterval = TimeSpan.FromMilliseconds(epoch) };
        }

        if (Number(section, "CallTimeoutSeconds") is { } call)
        {
            options = options with { CallTimeout = TimeSpan.FromSeconds(call) };
        }

        if (Number(section, "MiddlewareTimeoutMilliseconds") is { } middleware)
        {
            options = options with { MiddlewareTimeout = TimeSpan.FromMilliseconds(middleware) };
        }

        if (Number(section, "HealthIntervalSeconds") is { } health)
        {
            options = options with { HealthInterval = TimeSpan.FromSeconds(health) };
        }

        if (Number(section, "RestartWindowSeconds") is { } window)
        {
            options = options with { RestartWindow = TimeSpan.FromSeconds(window) };
        }

        if (Number(section, "HealthFailures") is { } failures)
        {
            options = options with { HealthFailures = (int)failures };
        }

        if (Number(section, "RestartLimit") is { } limit)
        {
            options = options with { RestartLimit = (int)limit };
        }

        return options;
    }

    /// <summary>
    /// 读取一个数。
    /// </summary>
    /// <remarks>
    /// 未配置为空，不是非负数则抛出。
    /// </remarks>
    private static double? Number(IConfiguration section, string name)
    {
        var text = section[name];
        if (text is null)
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a non-negative number, but was '{text}'.");
    }
}
