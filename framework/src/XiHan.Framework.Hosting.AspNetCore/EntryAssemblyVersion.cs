// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 入口程序集的版本
/// </summary>
/// <remarks>
/// 供启动 Banner 的应用一行使用：内核（Ring 1）不读程序集信息，由宿主包读出后填进 <c>StartupBannerOptions.ApplicationVersion</c>。<c>Hosting.CommandLine</c> 有一份相同的实现，同环之间不共享实现
/// </remarks>
internal static class EntryAssemblyVersion
{
    /// <summary>
    /// 读取入口程序集的版本
    /// </summary>
    /// <remarks>
    /// 优先取 <see cref="AssemblyInformationalVersionAttribute"/> 并去掉 <c>+</c> 之后的源码修订号，没有时取程序集版本；没有入口程序集时为 <see langword="null"/>。两者在 Native AOT 下都可用
    /// </remarks>
    internal static string? Resolve()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
        {
            return null;
        }

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            // SDK 缺省把提交哈希附在 + 之后，Banner 只要语义版本，与框架版本的写法一致
            var metadata = informational.IndexOf('+', StringComparison.Ordinal);
            return metadata < 0 ? informational : informational[..metadata];
        }

        return assembly.GetName().Version?.ToString();
    }
}
