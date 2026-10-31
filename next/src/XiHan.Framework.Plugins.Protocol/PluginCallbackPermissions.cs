// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 插件回调宿主时的权限判定。
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §5、<c>spec/plugin/wasm.md</c> §4：权限要在清单 <c>permissions</c> 里声明并且已被宿主授予才放行，清单是插件权限的唯一事实源，宿主授予而清单没有声明的部分不生效（RFC-0017）。
/// </remarks>
internal static class PluginCallbackPermissions
{
    /// <summary>
    /// 回调 <c>Host.Invoke</c> / <c>xihan.invoke</c> 需要的权限。
    /// </summary>
    public const string Pipeline = "capability:kernel.pipeline";

    /// <summary>
    /// 回调 <c>Host.Publish</c> / <c>xihan.publish</c> 需要的权限。
    /// </summary>
    public const string Messaging = "capability:messaging.queue";

    /// <summary>
    /// 该权限是否既在清单里声明又已被授予。
    /// </summary>
    public static bool IsAllowed(Manifest manifest, PermissionGrant granted, string permission)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(granted);
        ArgumentNullException.ThrowIfNull(permission);
        return manifest.Permissions.Contains(permission, StringComparer.Ordinal) && granted.IsGranted(permission);
    }
}
