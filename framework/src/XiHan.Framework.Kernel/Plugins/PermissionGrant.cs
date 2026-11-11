// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 宿主授予插件的权限集合
/// </summary>
/// <remarks>
/// 插件清单声明所需权限，宿主配置决定授予哪些，运行期由宿主强制
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionGrant
{
    /// <summary>
    /// 已授予的权限，序号比较
    /// </summary>
    private readonly HashSet<string> _granted;

    /// <summary>
    /// 创建授权集合
    /// </summary>
    public PermissionGrant(IEnumerable<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    /// <summary>
    /// 读取宿主配置节的授予前缀
    /// </summary>
    /// <remarks>
    /// <c>config:&lt;配置节&gt;</c>，配置节与清单 <c>contributes.config.section</c> 逐字相同（ADR-0177）
    /// </remarks>
    public const string ConfigurationPrefix = "config:";

    /// <summary>
    /// 读取宿主绑定的授予前缀
    /// </summary>
    /// <remarks>
    /// <c>binding:&lt;绑定名&gt;</c>，绑定名与清单 <c>bindings</c> 里的名字逐字相同（ADR-0177）
    /// </remarks>
    public const string BindingPrefix = "binding:";

    /// <summary>
    /// 空授权
    /// </summary>
    public static PermissionGrant Empty { get; } = new([]);

    /// <summary>
    /// 已授予的权限
    /// </summary>
    public IReadOnlySet<string> Granted => _granted;

    /// <summary>
    /// 是否已授予指定权限
    /// </summary>
    public bool IsGranted(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return _granted.Contains(permission);
    }

    /// <summary>
    /// 清单声明但未授予的权限
    /// </summary>
    public IReadOnlyList<string> Missing(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Permissions.Where(p => !_granted.Contains(p)).ToList();
    }

    /// <summary>
    /// 清单要读的宿主配置节与绑定中未授予的部分
    /// </summary>
    /// <remarks>
    /// 隔离运行的插件（进程外、Wasm）只拿得到运营方授予了的配置节与绑定：清单的 <c>contributes.config.section</c> 要求授予 <see cref="ConfigurationPrefix"/> 加配置节，<c>bindings</c> 的每一项要求授予 <see cref="BindingPrefix"/> 加绑定名。配置节里常有密钥、连接串与签名密钥，不能由插件自己点名（ADR-0177）。进程内插件是受信代码，本来就能读整个配置，不受此限
    /// </remarks>
    public IReadOnlyList<string> MissingConfigurationGrants(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var required = new List<string>();
        if (manifest.Contributes.Config is { } config)
        {
            required.Add(ConfigurationPrefix + config.Section);
        }

        required.AddRange(manifest.Bindings.Select(static binding => BindingPrefix + binding.Name));
        return required.Where(grant => !_granted.Contains(grant)).ToList();
    }
}
