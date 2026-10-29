// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。协议层错误随 JSON-RPC 错误对象的 <c>data.code</c> 返回。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpErrors
{
    /// <summary>
    /// 消息不是合法的 JSON 或不是合法的 JSON-RPC 2.0 请求。
    /// </summary>
    public const string InvalidMessage = "PROTOCOLS_VALIDATION_001";

    /// <summary>
    /// MCP 协议版本不受支持。
    /// </summary>
    public const string UnsupportedVersion = "PROTOCOLS_VALIDATION_002";

    /// <summary>
    /// tool 的参数不是 JSON 对象。
    /// </summary>
    public const string InvalidArguments = "PROTOCOLS_VALIDATION_003";

    /// <summary>
    /// 由操作 ID 推导或配置覆盖的 tool 名不合法。
    /// </summary>
    public const string InvalidToolName = "PROTOCOLS_VALIDATION_004";

    /// <summary>
    /// JSON-RPC 方法不受支持。
    /// </summary>
    public const string UnknownMethod = "PROTOCOLS_NOTFOUND_001";

    /// <summary>
    /// tool 未登记。
    /// </summary>
    public const string UnknownTool = "PROTOCOLS_NOTFOUND_002";

    /// <summary>
    /// 两个操作得到同一个 tool 名。
    /// </summary>
    public const string DuplicateToolName = "PROTOCOLS_CONFLICT_001";
}
