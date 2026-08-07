// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Web.Mcp.Filters;

/// <summary>
/// MCP 端点 API Key 过滤器（应用管理的 key；在 MCP 处理器/SSE 流开启前校验，不匹配即 401）
/// </summary>
/// <remarks>
/// 端点过滤器在路由与鉴权中间件之后、处理器之前运行一次;配合 <c>AllowAnonymous()</c> 绕过框架全局 FallbackPolicy,
/// 改由本 key 校验守门。定长比较防时序侧信道。接受请求头(默认 X-Api-Key)或 Authorization: Bearer。
/// </remarks>
public sealed class McpApiKeyEndpointFilter : IEndpointFilter
{
    private readonly byte[] _expectedKey;
    private readonly string _headerName;

    /// <summary>
    /// 构造函数
    /// </summary>
    public McpApiKeyEndpointFilter(string apiKey, string headerName)
    {
        _expectedKey = Encoding.UTF8.GetBytes(apiKey);
        _headerName = headerName;
    }

    /// <summary>
    /// 校验请求携带的 API Key，通过则继续执行后续过滤器与处理器，否则返回 401
    /// </summary>
    /// <param name="context">端点过滤器调用上下文</param>
    /// <param name="next">后续过滤器委托</param>
    /// <returns>校验失败返回未授权结果，否则返回后续处理器的结果</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var provided = request.Headers[_headerName].ToString();
        if (string.IsNullOrEmpty(provided))
        {
            var authorization = request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                provided = authorization["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expectedKey))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
