// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 依赖的一个能力。
/// </summary>
internal sealed record RequirementEntry
{
    /// <summary>
    /// 能力 ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 接口大版本下限。
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 是否可选。
    /// </summary>
    /// <remarks>
    /// 可选依赖缺失不阻止启动，特性进入 Degraded。
    /// </remarks>
    public bool Optional { get; set; }
}
