// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_info</c> 的输入。
/// </summary>
internal sealed record InfoRequest
{
    /// <summary>
    /// 要查的包、特性或能力。
    /// </summary>
    /// <remarks>
    /// 包 ID、短名（<c>Caching.Redis</c>）、清单 ID、特性类型名或能力 ID（可带 <c>@版本</c>）；为空时列出全部包。
    /// </remarks>
    public string? Name { get; set; }
}
