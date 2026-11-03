// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm.Instances;

/// <summary>
/// 一次导出函数调用的结果：成功时带响应字节，否则带原因
/// </summary>
/// <param name="Status">结局</param>
/// <param name="Response">成功时的 UTF-8 JSON 响应</param>
/// <param name="Detail">失败原因的原文（陷阱种类与消息等），只进日志</param>
/// <param name="Summary">可以写进错误的失败原因（陷阱种类、异常类型或框架自己写的文字）</param>
internal readonly record struct WasmCallOutcome(WasmCallStatus Status, byte[]? Response, string? Detail, string? Summary)
{
    /// <summary>
    /// 成功
    /// </summary>
    public static WasmCallOutcome Succeeded(byte[] response) => new(WasmCallStatus.Success, response, null, null);

    /// <summary>
    /// 失败
    /// </summary>
    /// <remarks>
    /// <paramref name="summary"/> 为空表示 <paramref name="detail"/> 是框架自己写的，可以原样写进错误
    /// </remarks>
    public static WasmCallOutcome Failed(WasmCallStatus status, string detail, string? summary = null) => new(status, null, detail, summary ?? detail);
}
