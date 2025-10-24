#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiAttribute
// Guid:e5f6g7h8-i9j0-4k1l-2m3n-4o5p6q7r8s9t
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Attributes;

/// <summary>
/// 动态 API 特性
/// 标记类或方法以配置动态 API 行为
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DynamicApiAttribute : Attribute
{
    /// <summary>
    /// 路由模板
    /// </summary>
    public string? RouteTemplate { get; set; }

    /// <summary>
    /// API 名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 是否启用动态 API
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// API 版本
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isEnabled">是否启用</param>
    public DynamicApiAttribute(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// 禁用动态 API 特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DisableDynamicApiAttribute : Attribute
{
}

/// <summary>
/// HTTP 方法特性基类
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public abstract class HttpMethodAttribute : Attribute
{
    /// <summary>
    /// HTTP 方法
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// 路由模板
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    protected HttpMethodAttribute(string method)
    {
        Method = method;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="template">路由模板</param>
    protected HttpMethodAttribute(string method, string template)
    {
        Method = method;
        Template = template;
    }
}

/// <summary>
/// HTTP GET 方法特性
/// </summary>
public class HttpGetAttribute : HttpMethodAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpGetAttribute() : base("GET")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="template">路由模板</param>
    public HttpGetAttribute(string template) : base("GET", template)
    {
    }
}

/// <summary>
/// HTTP POST 方法特性
/// </summary>
public class HttpPostAttribute : HttpMethodAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpPostAttribute() : base("POST")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="template">路由模板</param>
    public HttpPostAttribute(string template) : base("POST", template)
    {
    }
}

/// <summary>
/// HTTP PUT 方法特性
/// </summary>
public class HttpPutAttribute : HttpMethodAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpPutAttribute() : base("PUT")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="template">路由模板</param>
    public HttpPutAttribute(string template) : base("PUT", template)
    {
    }
}

/// <summary>
/// HTTP DELETE 方法特性
/// </summary>
public class HttpDeleteAttribute : HttpMethodAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpDeleteAttribute() : base("DELETE")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="template">路由模板</param>
    public HttpDeleteAttribute(string template) : base("DELETE", template)
    {
    }
}

/// <summary>
/// HTTP PATCH 方法特性
/// </summary>
public class HttpPatchAttribute : HttpMethodAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpPatchAttribute() : base("PATCH")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="template">路由模板</param>
    public HttpPatchAttribute(string template) : base("PATCH", template)
    {
    }
}

