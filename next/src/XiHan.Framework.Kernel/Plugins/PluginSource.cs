// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件来源：清单与程序集所在目录，以及宿主授予的权限。
/// </summary>
/// <param name="Directory">包含 <c>xihan.manifest.json</c> 的目录。</param>
/// <param name="Permissions">授予的权限。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PluginSource(string Directory, PermissionGrant Permissions)
{
    /// <summary>
    /// 目录。
    /// </summary>
    public string Directory { get; } = string.IsNullOrWhiteSpace(Directory) ? throw new ArgumentException("插件目录不能为空。", nameof(Directory)) : Directory;

    /// <summary>
    /// 授予的权限。
    /// </summary>
    public PermissionGrant Permissions { get; } = Permissions ?? throw new ArgumentNullException(nameof(Permissions));
}
