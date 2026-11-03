// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// Web 应用构建器上的装配
/// </summary>
/// <remarks>
/// 在内核的 <c>AddXiHan</c> 之前把 <c>XIHAN_CONFIG_PATH</c> 目录里的 JSON 文件作为可重载的配置源，插在 <c>appsettings.{环境}.json</c> 之后（RFC-0016）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// 装配 XiHan 应用，并装载挂载的配置目录（存在时）
    /// </summary>
    /// <remarks>
    /// 目录里的文件紧接最后一个 appsettings 文件源（<c>appsettings.json</c> 或 <c>appsettings.{环境}.json</c>）插入：覆盖 appsettings，又被其后的开发环境用户机密、环境变量、命令行参数与应用随后添加的配置源覆盖。构建器里没有 appsettings 文件源（关掉了默认配置）时追加在末尾。文件变化触发 <c>IOptionsMonitor</c> 重载
    /// </remarks>
    public static WebApplicationBuilder AddXiHan(this WebApplicationBuilder builder, Action<AppBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        AddConfigurationDirectory(builder.Configuration, builder.Environment.EnvironmentName);
        return HostApplicationBuilderExtensions.AddXiHan(builder, configure);
    }

    /// <summary>
    /// 把挂载目录里的 JSON 文件按装载顺序插到最后一个 appsettings 文件源之后
    /// </summary>
    private static void AddConfigurationDirectory(ConfigurationManager configuration, string environmentName)
    {
        var files = ConfigurationDirectory.Resolve();
        if (files.Count == 0)
        {
            return;
        }

        var sources = configuration.Sources;
        // 挂载目录是部署侧的文件配置，与 appsettings 同层：放在 appsettings.{环境}.json 之后才覆盖它；
        // 环境变量与命令行（以及开发环境的用户机密）在默认构建器里注册得更晚，因而仍覆盖目录里的文件
        var index = sources.Count;
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is FileConfigurationSource { Path: { } path } && ConfigurationDirectory.IsApplicationSettings(path, environmentName))
            {
                index = i + 1;
                break;
            }
        }

        foreach (var file in files)
        {
            var source = new JsonConfigurationSource { Path = file, Optional = true, ReloadOnChange = true };
            // 绝对路径要换成以所在目录为根的文件提供器，否则会按内容根目录去找
            source.ResolveFileProvider();
            sources.Insert(index++, source);
        }
    }
}
