// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// 一次导出函数调用的结果：成功时带响应字节，否则带原因。
/// </summary>
/// <param name="Status">结局。</param>
/// <param name="Response">成功时的 UTF-8 JSON 响应。</param>
/// <param name="Detail">失败原因（陷阱种类与消息等）。</param>
internal readonly record struct WasmCallOutcome(WasmCallStatus Status, byte[]? Response, string? Detail)
{
    /// <summary>
    /// 成功。
    /// </summary>
    public static WasmCallOutcome Succeeded(byte[] response) => new(WasmCallStatus.Success, response, null);

    /// <summary>
    /// 失败。
    /// </summary>
    public static WasmCallOutcome Failed(WasmCallStatus status, string detail) => new(status, null, detail);
}
