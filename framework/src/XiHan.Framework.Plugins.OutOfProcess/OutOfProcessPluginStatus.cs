// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件的诊断快照
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OutOfProcessPluginStatus
{
    /// <summary>
    /// 清单、状态与错误
    /// </summary>
    public required PluginStatus Status { get; init; }

    /// <summary>
    /// 当前进程 ID
    /// </summary>
    /// <remarks>
    /// 没有运行中的进程为空
    /// </remarks>
    public int? ProcessId { get; init; }

    /// <summary>
    /// 协商出的协议版本
    /// </summary>
    /// <remarks>
    /// 尚未握手为空
    /// </remarks>
    public uint? ProtocolVersion { get; init; }

    /// <summary>
    /// 崩溃后自动重启的累计次数
    /// </summary>
    public required int Restarts { get; init; }

    /// <summary>
    /// 是否懒激活（清单 <c>activation</c> 不含 <c>onStartup</c>）
    /// </summary>
    public required bool Lazy { get; init; }
}
