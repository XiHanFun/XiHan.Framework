// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpProtocolCapabilities
{
    /// <summary>MCP 协议：操作作为 tool 经 Model Context Protocol 暴露。</summary>
    public const string Mcp = "protocols.mcp";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
