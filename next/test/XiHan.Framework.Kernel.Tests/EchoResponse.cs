// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 回声操作的响应：回声文本与处理它的调用的 trace-id。
/// </summary>
/// <param name="Echo">回声文本。</param>
/// <param name="TraceId">调用的 trace-id。</param>
internal sealed record EchoResponse(string Echo, string TraceId);
