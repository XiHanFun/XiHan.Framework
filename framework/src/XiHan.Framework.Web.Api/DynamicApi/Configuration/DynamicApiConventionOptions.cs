#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiConventionOptions
// Guid:f6g7h8i9-j0k1-4l2m-3n4o-5p6q7r8s9t0u
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Configuration;

/// <summary>
/// 动态 API 约定配置
/// </summary>
public class DynamicApiConventionOptions
{
    /// <summary>
    /// HTTP 方法约定映射
    /// Key: 方法名称前缀, Value: HTTP 方法
    /// </summary>
    public Dictionary<string, string> HttpMethodConventions { get; set; } = new()
    {
        { "Get", "GET" },
        { "Retrieve", "GET" },
        { "Fetch", "GET" },
        { "Find", "GET" },
        { "Query", "GET" },
        { "List", "GET" },
        { "Search", "GET" },
        { "Create", "POST" },
        { "Add", "POST" },
        { "Insert", "POST" },
        { "Update", "PUT" },
        { "Edit", "PUT" },
        { "Modify", "PUT" },
        { "Delete", "DELETE" },
        { "Remove", "DELETE" },
        { "Destroy", "DELETE" },
        { "Patch", "PATCH" },
        { "PartialUpdate", "PATCH" }
    };

    /// <summary>
    /// 是否使用 PascalCase 路由
    /// true: GetUsers -> GetUsers
    /// false: GetUsers -> get-users
    /// </summary>
    public bool UsePascalCaseRoutes { get; set; } = false;

    /// <summary>
    /// 是否使用小写路由
    /// </summary>
    public bool UseLowercaseRoutes { get; set; } = false;

    /// <summary>
    /// 路由分隔符
    /// </summary>
    public string RouteSeparator { get; set; } = "";
}
