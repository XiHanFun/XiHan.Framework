// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Mcp.Server;

/// <summary>
/// JSON-RPC 2.0 规范预定义的错误码。
/// </summary>
internal static class JsonRpcErrorCodes
{
    /// <summary>
    /// 请求不是合法的 JSON。
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// 请求不是合法的请求对象。
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// 方法不存在。
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// 参数不合法。
    /// </summary>
    public const int InvalidParams = -32602;
}
