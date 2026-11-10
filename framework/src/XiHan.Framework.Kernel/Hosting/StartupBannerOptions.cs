// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 启动 Banner 的选项
/// </summary>
/// <remarks>
/// 宿主启动时（<c>IHostedLifecycleService.StartingAsync</c>）打印字符画 Logo、框架版本、应用、环境、运行时与操作系统。<see cref="Enabled"/> 从配置节 <c>XiHan:Kernel:Banner</c> 显式读取，其余属性只能在代码里设置。内核在 <see cref="AppBuilder.Build"/> 里以 <c>TryAddSingleton</c> 登记，应用先登记的实例不被覆盖；命令行宿主把它换成缺省关闭、写标准错误
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record StartupBannerOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Kernel:Banner";

    /// <summary>
    /// 是否在宿主启动时打印
    /// </summary>
    /// <remarks>
    /// 配置键 <c>XiHan:Kernel:Banner:Enabled</c>，缺省 <see langword="true"/>
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 写入目标
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时在打印时取 <see cref="Console.Out"/>。目标是没有重定向的控制台、且没有设置环境变量 <c>NO_COLOR</c> 时 Logo 才带颜色
    /// </remarks>
    public TextWriter? Output { get; init; }

    /// <summary>
    /// 应用版本
    /// </summary>
    /// <remarks>
    /// 打印在应用名之后；为 <see langword="null"/> 时只打印应用名。<c>Hosting.AspNetCore</c> 与 <c>Hosting.CommandLine</c> 的 <c>AddXiHan</c> 从入口程序集读取并填入，内核自己不读程序集信息
    /// </remarks>
    public string? ApplicationVersion { get; init; }

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 配置里没有的键取 <paramref name="defaults"/> 的值（为 <see langword="null"/> 时取缺省实例），宿主用它给出自己的缺省（命令行宿主缺省关闭），配置里显式写的值仍然优先。非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    /// <param name="configuration">宿主配置</param>
    /// <param name="defaults">配置里没有的键取它的值</param>
    public static StartupBannerOptions FromConfiguration(IConfiguration configuration, StartupBannerOptions? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        defaults ??= new StartupBannerOptions();
        var raw = configuration.GetSection(ConfigurationSection)["Enabled"];
        if (raw is null)
        {
            return defaults;
        }

        // 显式解析而不是 Bind：保持零反射与 AOT 友好，写错的值在启动时就失败，不静默当成关闭
        return bool.TryParse(raw, out var enabled)
            ? defaults with { Enabled = enabled }
            : throw new ArgumentException($"'{ConfigurationSection}:Enabled' must be 'true' or 'false', but the value is '{raw}'.");
    }
}
