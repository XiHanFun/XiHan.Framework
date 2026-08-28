// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Contexts;

/// <summary>
/// 请求上下文访问器
/// </summary>
public sealed class RequestContextAccessor : IRequestContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public RequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 当前请求上下文，存取于 HttpContext.Items，不在请求中时读取返回 null、写入被忽略
    /// </summary>
    public RequestContext? Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            return httpContext.Items.TryGetValue(XiHanWebApiConstants.RequestContextItemKey, out var value)
                ? value as RequestContext
                : null;
        }
        set
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return;
            }

            if (value is null)
            {
                httpContext.Items.Remove(XiHanWebApiConstants.RequestContextItemKey);
            }
            else
            {
                httpContext.Items[XiHanWebApiConstants.RequestContextItemKey] = value;
            }
        }
    }
}
