// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// 对一条 JSON-RPC 消息的答复。
/// </summary>
/// <param name="Body">JSON-RPC 响应对象的 UTF-8 JSON。</param>
/// <param name="IsMalformed">消息本身不合法（非 JSON 或非 JSON-RPC 2.0 请求）；HTTP 传输据此回 400。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpReply(ReadOnlyMemory<byte> Body, bool IsMalformed);
