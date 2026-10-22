// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 的 Streamable HTTP 传输端点：一个 <c>POST</c> 端点收 JSON-RPC 消息，答复以 <c>application/json</c> 返回（不开 SSE 流，不建会话；<c>GET</c> / <c>DELETE</c> 为 405）。
/// 端点经过 <c>UseXiHan()</c> 的内核管道：请求的凭据先在外层认证（凭据无效时整个请求以 401 Problem Details 拒绝），每个 <c>tools/call</c> 再以自己的作用域与上下文经完整管道执行。必须放在 <c>UseXiHan()</c> 之后。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class McpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射 MCP 端点；需要 <see cref="McpProtocolFeature"/> 已装配。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var server = endpoints.ServiceProvider.GetService<McpServer>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanMcp() requires the MCP protocol feature; add UseFeature<McpProtocolFeature>() to the application.");

        return endpoints.MapPost(pattern, httpContext => HandleAsync(httpContext, server)).WithName("xihan.protocols.mcp");
    }

    /// <summary>
    /// 端点处理：校验协议版本头，读请求体，交给服务端；通知回 202，不合法的消息回 400，其余 200。
    /// </summary>
    private static async Task HandleAsync(HttpContext httpContext, McpServer server)
    {
        if (!httpContext.GetXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "The MCP endpoint requires UseXiHan() to run before routing; the invocation context is missing.");
        }

        // 协议版本头缺失按当前版本处理（规范允许）；给了不支持的版本按规范回 400
        var version = httpContext.Request.Headers[McpServer.ProtocolVersionHeader].ToString();
        if (version.Length > 0 && !string.Equals(version, McpServer.ProtocolVersion, StringComparison.Ordinal))
        {
            await WriteAsync(httpContext, StatusCodes.Status400BadRequest, McpServer.UnsupportedVersionReply(version), context.CancellationToken).ConfigureAwait(false);
            return;
        }

        using var body = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);

        // 每个 tools/call 在自己的服务作用域里执行：工作单元等作用域服务不与本次 HTTP 请求的外层调用共享
        await using var scope = httpContext.RequestServices.CreateAsyncScope();
        var reply = await server.HandleAsync(body.ToArray(), context, () => HttpInvocation.CreateContext(httpContext, scope.ServiceProvider)).ConfigureAwait(false);
        if (!reply.TryGetValue(out var response))
        {
            httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }

        await WriteAsync(httpContext, response.IsMalformed ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK, response.Body, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 写 JSON 响应。
    /// </summary>
    private static async Task WriteAsync(HttpContext httpContext, int statusCode, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.Body.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }
}
