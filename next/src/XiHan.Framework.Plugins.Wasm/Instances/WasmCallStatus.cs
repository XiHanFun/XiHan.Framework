// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Instances;

/// <summary>
/// 一次导出函数调用的结局。
/// </summary>
internal enum WasmCallStatus
{
    /// <summary>
    /// 得到响应。
    /// </summary>
    Success = 0,

    /// <summary>
    /// 导出函数返回 0：插件无法给出响应，按崩溃处理。
    /// </summary>
    NoResponse = 1,

    /// <summary>
    /// 客体内存分配失败或响应越界：沙箱资源不足。
    /// </summary>
    Exhausted = 2,

    /// <summary>
    /// 到达截止时间被 epoch 中断。
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// 陷阱（栈溢出、内存越界、unreachable 等）。
    /// </summary>
    Trapped = 4,

    /// <summary>
    /// wasmtime 报告的其他错误。
    /// </summary>
    Faulted = 5,
}
