// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 需要宿主注入的一个资源绑定
/// </summary>
internal sealed record BindingEntry
{
    /// <summary>
    /// 绑定名（<c>XiHan:Bindings:&lt;名字&gt;</c>）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 资源类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; }
}
