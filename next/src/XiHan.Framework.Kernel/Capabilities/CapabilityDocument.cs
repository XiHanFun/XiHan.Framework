// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>provides</c> 一项的 JSON 形态。版本以字符串承载，转换时再解析为正整数。
/// </summary>
internal sealed class CapabilityDocument
{
    /// <summary>
    /// 能力 ID。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 接口版本的原始文本。
    /// </summary>
    public string? Version { get; set; }
}
