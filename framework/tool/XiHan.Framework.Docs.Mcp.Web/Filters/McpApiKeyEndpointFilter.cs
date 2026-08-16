// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Docs.Mcp.Web.Filters;

/// <summary>
/// MCP 端点 API Key 过滤器（部署方管理的 key；在 MCP 处理器/SSE 流开启前校验，不匹配即 401）
/// </summary>
/// <remarks>
/// 端点过滤器在路由与鉴权中间件之后、处理器之前运行一次；配合 <c>AllowAnonymous()</c> 绕过可能存在的全局
/// FallbackPolicy，改由本 key 校验守门。定长比较防时序侧信道。接受请求头(默认 X-Api-Key)或 Authorization: Bearer。
/// <para>
/// <b>这是 <c>framework/src/XiHan.Framework.Web.Mcp/Filters/McpApiKeyEndpointFilter.cs</c> 的一份刻意复制，
/// 不是疏忽。</b>复用那一份就得引用 <c>XiHan.Framework.Web.Mcp</c>，而它会把 <c>XiHan.Framework.AI</c> 与
/// <c>XiHan.Framework.Web.Core</c>——也就是整套模块系统——拖进这个本该独立的文档服务端。
/// 代价是两份安全代码可能各自漂移；按住它们的是
/// <c>framework/test/XiHan.Framework.Docs.Mcp.Web.Tests/</c> 里的鉴权用例：
/// 定长比较、Bearer 回退、多值请求头这三条行为逐条有断言，改坏任何一条都会变红。
/// 两边任何一份改动了校验逻辑，另一份必须同步。
/// </para>
/// </remarks>
public sealed class McpApiKeyEndpointFilter : IEndpointFilter
{
    private readonly byte[] _expectedKey;
    private readonly string _headerName;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="apiKey">期望的访问密钥</param>
    /// <param name="headerName">携带密钥的请求头名</param>
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
