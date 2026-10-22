// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 停机。
/// </summary>
internal sealed record ShutdownSettings
{
    /// <summary>
    /// 内核 Draining 的宽限（秒），写进 <c>XiHan:Kernel:StopTimeoutSeconds</c>；k8s 的 <c>terminationGracePeriodSeconds</c> 在此之上多 5 秒。
    /// </summary>
    public int GraceSeconds { get; set; } = 30;

    /// <summary>
    /// kubelet 终止前的宽限（秒）。
    /// </summary>
    public int TerminationGracePeriodSeconds => GraceSeconds + 5;
}
