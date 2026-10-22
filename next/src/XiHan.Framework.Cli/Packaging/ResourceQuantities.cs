// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// CPU 与内存数量（k8s 数量文法，如 <c>500m</c>、<c>512Mi</c>）。
/// </summary>
internal sealed record ResourceQuantities
{
    /// <summary>
    /// CPU。
    /// </summary>
    public string? Cpu { get; set; }

    /// <summary>
    /// 内存。
    /// </summary>
    public string? Memory { get; set; }
}
