// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 宿主授予插件的权限集合。插件清单声明所需权限，宿主配置决定授予哪些，运行期由宿主强制。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionGrant
{
    private readonly HashSet<string> _granted;

    /// <summary>
    /// 创建授权集合。
    /// </summary>
    public PermissionGrant(IEnumerable<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    /// <summary>
    /// 空授权。
    /// </summary>
    public static PermissionGrant Empty { get; } = new([]);

    /// <summary>
    /// 已授予的权限。
    /// </summary>
    public IReadOnlySet<string> Granted => _granted;

    /// <summary>
    /// 是否已授予指定权限。
    /// </summary>
    public bool IsGranted(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return _granted.Contains(permission);
    }

    /// <summary>
    /// 清单声明但未授予的权限。
    /// </summary>
    public IReadOnlyList<string> Missing(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Permissions.Where(p => !_granted.Contains(p)).ToList();
    }
}
