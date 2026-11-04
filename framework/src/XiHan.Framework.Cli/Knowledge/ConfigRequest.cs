// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// <c>xihan_framework_config</c> 的输入
/// </summary>
internal sealed record ConfigRequest
{
    /// <summary>
    /// 要查的配置节、键、包或 Options 类
    /// </summary>
    /// <remarks>
    /// 节路径（含其下的子节）、完整的键路径、包 ID 或短名、Options 类名；为空时列出全部配置节
    /// </remarks>
    public string? Query { get; set; }
}
