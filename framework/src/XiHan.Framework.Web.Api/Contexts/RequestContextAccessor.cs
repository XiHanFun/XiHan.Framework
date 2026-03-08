#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RequestContextAccessor
// Guid:6d2a9e2c-3f46-4f48-8c45-3a3f0a3a9b3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Http;
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

    /// <inheritdoc />
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
