// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 中 <c>requires</c> 一项的 JSON 形态。
/// </summary>
/// <remarks>
/// 版本以字符串承载，允许 <c>^</c> 前缀。
/// </remarks>
internal sealed class RequirementDocument
{
    /// <summary>
    /// 能力 ID。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 接口版本的原始文本。
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 是否可选。
    /// </summary>
    /// <remarks>
    /// 缺省视为必需。
    /// </remarks>
    public bool? Optional { get; set; }
}
