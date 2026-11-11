// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件来源：清单与程序集所在目录，以及宿主授予的权限
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PluginSource
{
    /// <summary>
    /// 创建插件来源
    /// </summary>
    /// <param name="directory">包含 <c>xihan.manifest.json</c> 的目录</param>
    /// <param name="permissions">授予的权限</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> 为空或空白</exception>
    /// <exception cref="ArgumentNullException"><paramref name="permissions"/> 为 <see langword="null"/></exception>
    public PluginSource(string directory, PermissionGrant permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(permissions);
        Directory = directory;
        Permissions = permissions;
    }

    /// <summary>
    /// 目录
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// 授予的权限
    /// </summary>
    public PermissionGrant Permissions { get; }
}
