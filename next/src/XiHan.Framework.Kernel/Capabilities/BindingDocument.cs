// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>bindings</c> 一项的 JSON 形态。只承载原始文本，格式校验在转换为 <c>Binding</c> 时进行。
/// </summary>
internal sealed class BindingDocument
{
    /// <summary>
    /// 绑定名。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 资源类型。
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 是否必需；缺省视为必需。
    /// </summary>
    public bool? Required { get; set; }
}
