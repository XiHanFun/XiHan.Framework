// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 探针。
/// </summary>
internal sealed record ProbeSettings
{
    /// <summary>
    /// 启动探针允许的总时长（秒）。
    /// </summary>
    public int StartupSeconds { get; set; } = 60;

    /// <summary>
    /// 探测周期（秒）。
    /// </summary>
    public int PeriodSeconds { get; set; } = 10;

    /// <summary>
    /// 单次探测超时（秒）。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// 启动探针的失败阈值：总时长除以周期，向上取整。
    /// </summary>
    public int StartupFailureThreshold => (int)Math.Ceiling(StartupSeconds / (double)PeriodSeconds);
}
