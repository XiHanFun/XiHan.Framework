// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，特性装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "protocols.mcp";

    /// <summary>
    /// 请求体不是合法的 JSON。
    /// </summary>
    public const string MessageNotJson = "protocols.mcp.message_not_json";

    /// <summary>
    /// 消息不是合法的 JSON-RPC 2.0 请求。
    /// </summary>
    public const string MessageInvalid = "protocols.mcp.message_invalid";

    /// <summary>
    /// MCP 协议版本不受支持。参数：<c>version</c>、<c>supported</c>。
    /// </summary>
    public const string VersionUnsupported = "protocols.mcp.version_unsupported";

    /// <summary>
    /// 方法不受支持。参数：<c>method</c>。
    /// </summary>
    public const string MethodUnknown = "protocols.mcp.method_unknown";

    /// <summary>
    /// tool 未登记。参数：<c>tool</c>。
    /// </summary>
    public const string ToolUnknown = "protocols.mcp.tool_unknown";

    /// <summary>
    /// tool 的参数不是 JSON 对象。参数：<c>tool</c>。
    /// </summary>
    public const string ArgumentsInvalid = "protocols.mcp.arguments_invalid";

    /// <summary>
    /// 命令行宿主里 <c>mcp</c> 命令（stdio 传输）的说明。
    /// </summary>
    public const string StdioDescription = "protocols.mcp.stdio_description";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(McpMessages));
}
