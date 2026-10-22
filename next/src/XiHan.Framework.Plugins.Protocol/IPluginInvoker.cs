// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 把代理操作的调用交给某种载体（子进程、Wasm 实例）的运行时。
/// </summary>
internal interface IPluginInvoker
{
    /// <summary>
    /// 调用插件的一个操作；载荷与结果都是原样搬运的 JSON 字节。
    /// </summary>
    ValueTask<Result<object>> InvokeAsync(string operation, ReadOnlyMemory<byte> payload, InvocationContext context);
}
