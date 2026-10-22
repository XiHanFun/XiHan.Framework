// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 自动伸缩（HPA）。
/// </summary>
internal sealed record ScalingSettings
{
    /// <summary>
    /// 最少副本。
    /// </summary>
    public int Min { get; set; } = 1;

    /// <summary>
    /// 最多副本。
    /// </summary>
    public int Max { get; set; } = 1;

    /// <summary>
    /// 目标 CPU 利用率（百分比）。
    /// </summary>
    public int CpuPercent { get; set; } = 70;
}
