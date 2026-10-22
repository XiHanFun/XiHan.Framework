// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件的诊断快照。
/// </summary>
/// <param name="Status">清单、状态与错误。</param>
/// <param name="ProcessId">当前进程 ID；没有运行中的进程为空。</param>
/// <param name="ProtocolVersion">协商出的协议版本；尚未握手为空。</param>
/// <param name="Restarts">崩溃后自动重启的累计次数。</param>
/// <param name="Lazy">是否懒激活（清单 <c>activation</c> 不含 <c>onStartup</c>）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OutOfProcessPluginStatus(PluginStatus Status, int? ProcessId, uint? ProtocolVersion, int Restarts, bool Lazy);
