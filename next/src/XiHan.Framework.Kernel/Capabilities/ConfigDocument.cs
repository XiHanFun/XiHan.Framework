// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>contributes.config</c> 的 JSON 形态。
/// </summary>
internal sealed class ConfigDocument
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public string? Section { get; set; }

    /// <summary>
    /// JSON Schema 的相对路径。
    /// </summary>
    public string? Schema { get; set; }
}
