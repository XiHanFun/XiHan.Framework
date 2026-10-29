// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// Web 应用构建器上的装配。
/// </summary>
/// <remarks>
/// 在内核的 <c>AddXiHan</c> 之前把 <c>XIHAN_CONFIG_PATH</c> 目录里的 JSON 文件追加为可重载的配置源。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// 装配 XiHan 应用，并装载挂载的配置目录（存在时）。
    /// </summary>
    /// <remarks>
    /// 目录里的文件晚于 appsettings 装载因而优先，文件变化触发 <c>IOptionsMonitor</c> 重载。
    /// </remarks>
    public static WebApplicationBuilder AddXiHan(this WebApplicationBuilder builder, Action<AppBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        foreach (var file in ConfigurationDirectory.Resolve())
        {
            builder.Configuration.AddJsonFile(file, optional: true, reloadOnChange: true);
        }

        return HostApplicationBuilderExtensions.AddXiHan(builder, configure);
    }
}
