// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Server;

namespace XiHan.Framework.Protocols.Mcp.Transports;

/// <summary>
/// MCP 的 Streamable HTTP 传输端点
/// </summary>
/// <remarks>
/// 一个 <c>POST</c> 端点收 JSON-RPC 消息，答复以 <c>application/json</c> 返回（不开 SSE 流，不建会话；<c>GET</c> / <c>DELETE</c> 为 405）。端点经过 <c>UseXiHan()</c> 的内核管道：请求的凭据先在外层认证（凭据无效时整个请求以 401 Problem Details 拒绝），每个 <c>tools/call</c> 再以自己的作用域与上下文经完整管道执行。必须放在 <c>UseXiHan()</c> 之后
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射 MCP 端点
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="McpProtocolFeature"/> 已装配
    /// </remarks>
    public static IEndpointConventionBuilder MapXiHanMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var server = endpoints.ServiceProvider.GetService<McpServer>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanMcp() requires the MCP protocol feature; add UseFeature<McpProtocolFeature>() to the application.");
        var options = endpoints.ServiceProvider.GetRequiredService<McpProtocolOptions>();

        return endpoints.MapPost(pattern, httpContext => HandleAsync(httpContext, server, options)).WithName("xihan.protocols.mcp");
    }

    /// <summary>
    /// 端点处理
    /// </summary>
    /// <remarks>
    /// 先做入口校验（源、内容类型、请求体大小，ADR-0199），再校验协议版本头，读请求体，交给服务端；通知回 202，不合法的消息回 400，其余 200
    /// </remarks>
    private static async Task HandleAsync(HttpContext httpContext, McpServer server, McpProtocolOptions options)
    {
        if (!httpContext.FindXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "The MCP endpoint requires UseXiHan() to run after routing and before the endpoint executes; the invocation context is missing.");
        }

        // 源先于一切：浏览器里的网页借 DNS 重绑定能把请求发到本机端口，带着别处的 Origin，规范要求服务端拒绝（403）
        var origin = httpContext.Request.Headers.Origin;
        if (origin.Count > 0 && !options.IsAllowedOrigin(origin.ToString()))
        {
            await WriteAsync(httpContext, StatusCodes.Status403Forbidden, McpServer.TransportErrorReply(McpErrors.OriginDenied, McpMessages.OriginDenied.L(("origin", origin.ToString()))), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // 只收 JSON：text/plain 与表单是浏览器不经预检就能跨站发出的简单请求
        var contentType = httpContext.Request.ContentType;
        if (!IsJson(contentType))
        {
            await WriteAsync(httpContext, StatusCodes.Status415UnsupportedMediaType, McpServer.TransportErrorReply(McpErrors.ContentTypeInvalid, McpMessages.ContentTypeInvalid.L(("contentType", contentType ?? string.Empty))), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // 协议版本头缺失按当前版本处理（规范允许）；给了不支持的版本按规范回 400
        var version = httpContext.Request.Headers[McpServer.ProtocolVersionHeader].ToString();
        if (version.Length > 0 && !string.Equals(version, McpServer.ProtocolVersion, StringComparison.Ordinal))
        {
            await WriteAsync(httpContext, StatusCodes.Status400BadRequest, McpServer.UnsupportedVersionReply(version), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (await ReadBodyAsync(httpContext.Request, options.MaxRequestBodyBytes, context.CancellationToken).ConfigureAwait(false) is not { } body)
        {
            await WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge, McpServer.TransportErrorReply(McpErrors.BodyTooLarge, McpMessages.BodyTooLarge.L(("max", options.MaxRequestBodyBytes))), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // 每个 tools/call 在自己的服务作用域里执行：工作单元等作用域服务不与本次 HTTP 请求的外层调用共享
        await using var scope = httpContext.RequestServices.CreateAsyncScope();
        var reply = await server.HandleAsync(body, context, () => HttpInvocation.CreateContext(httpContext, scope.ServiceProvider)).ConfigureAwait(false);
        if (!reply.TryGetValue(out var response))
        {
            httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }

        await WriteAsync(httpContext, response.IsMalformed ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK, response.Body, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 内容类型是否 <c>application/json</c>
    /// </summary>
    /// <remarks>
    /// 不分大小写，忽略参数（如 <c>charset</c>）
    /// </remarks>
    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';', StringComparison.Ordinal);
        var mediaType = (separator < 0 ? contentType : contentType[..separator]).Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读请求体，最多 <paramref name="max"/> 字节
    /// </summary>
    /// <remarks>
    /// 超过时为 <see langword="null"/>。<c>Content-Length</c> 已经超过的不读；没有声明长度的边读边计数，读到上限多一个字节即停
    /// </remarks>
    private static async ValueTask<byte[]?> ReadBodyAsync(HttpRequest request, long max, CancellationToken cancellationToken)
    {
        if (request.ContentLength > max)
        {
            return null;
        }

        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            // 计数在写进内存之前：超过上限的那一段不留
            if (memory.Length + read > max)
            {
                return null;
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    /// <summary>
    /// 写 JSON 响应
    /// </summary>
    private static async Task WriteAsync(HttpContext httpContext, int statusCode, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.Body.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }
}
