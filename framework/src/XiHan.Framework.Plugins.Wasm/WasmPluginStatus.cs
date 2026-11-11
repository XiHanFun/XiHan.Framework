// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件的诊断快照
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed record WasmPluginStatus
{
    /// <summary>
    /// 清单、状态与错误
    /// </summary>
    public required PluginStatus Status { get; init; }

    /// <summary>
    /// 模块报告的 ABI 版本
    /// </summary>
    /// <remarks>
    /// 尚未实例化为空
    /// </remarks>
    public uint? AbiVersion { get; init; }

    /// <summary>
    /// 协商出的协议版本
    /// </summary>
    /// <remarks>
    /// 尚未握手为空
    /// </remarks>
    public uint? ProtocolVersion { get; init; }

    /// <summary>
    /// 崩溃后自动重建实例的累计次数
    /// </summary>
    public required int Restarts { get; init; }

    /// <summary>
    /// 是否懒激活（清单 <c>activation</c> 不含 <c>onStartup</c>）
    /// </summary>
    public required bool Lazy { get; init; }
}
