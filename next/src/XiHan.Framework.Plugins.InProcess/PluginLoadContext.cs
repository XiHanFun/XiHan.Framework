// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Runtime.Loader;

namespace XiHan.Framework.Plugins.InProcess;

/// <summary>
/// 插件的可收集加载上下文：框架与 .NET 基础程序集回落到默认上下文以共享类型，插件私有依赖按 <c>.deps.json</c> 隔离解析。
/// </summary>
internal sealed class PluginLoadContext(string name, string assemblyPath) : AssemblyLoadContext(name, isCollectible: true)
{
    private static readonly string[] _sharedPrefixes = ["XiHan.Framework.", "Microsoft.Extensions.", "System.", "netstandard", "mscorlib"];

    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name ?? string.Empty;
        if (_sharedPrefixes.Any(prefix => simpleName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
