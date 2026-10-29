// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Runtime.Loader;

namespace XiHan.Framework.Plugins.InProcess;

/// <summary>
/// 插件的可收集加载上下文。
/// </summary>
/// <remarks>
/// 框架与 .NET 基础程序集回落到默认上下文以共享类型，插件私有依赖按 <c>.deps.json</c> 隔离解析。
/// </remarks>
internal sealed class PluginLoadContext(string name, string assemblyPath) : AssemblyLoadContext(name, isCollectible: true)
{
    /// <summary>
    /// 必须与宿主共享的程序集名前缀。
    /// </summary>
    /// <remarks>
    /// 这些程序集交给默认上下文，保证 <c>IPlugin</c> 等类型在两边是同一个类型。
    /// </remarks>
    private static readonly string[] _sharedPrefixes = ["XiHan.Framework.", "Microsoft.Extensions.", "System.", "netstandard", "mscorlib"];

    /// <summary>
    /// 按插件自己的 <c>.deps.json</c> 与运行时配置解析托管和原生依赖。
    /// </summary>
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    /// <summary>
    /// 解析托管程序集。
    /// </summary>
    /// <remarks>
    /// 共享前缀返回 <c>null</c> 交给默认上下文；其余按插件目录解析，解析不到也返回 <c>null</c> 让运行时继续回落。
    /// </remarks>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 返回 null 即回落到默认上下文；若在这里自行加载框架程序集，会产生第二份类型身份，插件实现的接口将无法被宿主识别
        var simpleName = assemblyName.Name ?? string.Empty;
        if (_sharedPrefixes.Any(prefix => simpleName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <summary>
    /// 解析原生库：只在插件目录内查找，找不到返回 <see cref="IntPtr.Zero"/> 让运行时按默认探测继续。
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
