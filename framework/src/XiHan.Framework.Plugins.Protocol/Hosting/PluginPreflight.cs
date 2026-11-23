// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 启动任何插件代码之前的检查里两个宿主相同的部分：授予与入口
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（ADR-0260）。一项清单权限怎样才算授予、入口怎样解析成要启动的文件是载体的事，留在各自的宿主里：进程外只看宿主授予并在 Windows 上补 <c>.exe</c>，Wasm 的 <c>fs.*</c> 看映射、<c>net.outbound:</c> 永不授予
/// </remarks>
internal static class PluginPreflight
{
    /// <summary>
    /// 核对清单要的授予都在、入口写了，返回清单里的入口
    /// </summary>
    /// <remarks>
    /// 缺授予为 <c>PLUGINS_DENIED_001</c>，列出缺的每一项；没有入口为 <c>PLUGINS_VALIDATION_001</c>。先判授予再判入口
    /// </remarks>
    /// <param name="source">插件来源（目录与宿主的授予）</param>
    /// <param name="manifest">清单</param>
    /// <param name="isGranted">清单 <c>permissions</c> 里的一项是否已授予，由载体判定</param>
    public static Result<string> Check(PluginSource source, Manifest manifest, Func<string, bool> isGranted)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(isGranted);

        // 清单声明的权限，以及它要读的宿主配置节与绑定，都要运营方授予：配置节里有密钥，插件不能自己点名（ADR-0177）
        var missing = manifest.Permissions.Where(permission => !isGranted(permission)).Concat(source.Permissions.MissingConfigurationGrants(manifest)).ToList();
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        return manifest.Entry;
    }

    /// <summary>
    /// 入口指向的文件不存在
    /// </summary>
    /// <param name="path">宿主要启动的文件路径，原样写进错误</param>
    public static Error EntryMissing(string path)
        => Error.NotFound(PluginErrors.NotFound, PluginMessages.ExecutableMissing, MessageArguments.Of(("path", path)));
}
