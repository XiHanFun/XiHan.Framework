// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 一个资源绑定。
/// </summary>
internal sealed record BindingSettings
{
    /// <summary>
    /// 类型：<c>connection:relational</c>、<c>cache:distributed</c>、<c>queue</c>、<c>blob</c>。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// k8s Secret 名；为空时用 <c>&lt;应用&gt;-&lt;绑定&gt;</c>。
    /// </summary>
    public string? Secret { get; set; }
}
