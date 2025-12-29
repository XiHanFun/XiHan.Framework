#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiOptions
// Guid:f6g7h8i9-j0k1-4l2m-3n4o-5p6q7r8s9t0u
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Configuration;

/// <summary>
/// 动态 API 配置选项
/// </summary>
public class DynamicApiOptions
{
    /// <summary>
    /// 是否启用动态 API
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 默认路由前缀
    /// </summary>
    public string DefaultRoutePrefix { get; set; } = "api";

    /// <summary>
    /// 默认 API 版本
    /// </summary>
    public string? DefaultApiVersion { get; set; }

    /// <summary>
    /// 是否启用 API 版本控制
    /// </summary>
    public bool EnableApiVersioning { get; set; } = false;

    /// <summary>
    /// 是否启用批量操作
    /// </summary>
    public bool EnableBatchOperations { get; set; } = true;

    /// <summary>
    /// 批量操作最大数量
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// 是否移除服务名称的后缀
    /// 例如: UserAppService -> User
    /// </summary>
    public bool RemoveServiceSuffix { get; set; } = true;

    /// <summary>
    /// 要移除的服务名称后缀列表
    /// </summary>
    public List<string> ServiceSuffixes { get; set; } = ["ApplicationService", "Service"];

    /// <summary>
    /// 约定配置
    /// </summary>
    public DynamicApiConventionOptions Conventions { get; set; } = new();

    /// <summary>
    /// 路由配置
    /// </summary>
    public DynamicApiRouteOptions Routes { get; set; } = new();
}

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

/// <summary>
/// 动态 API 路由配置
/// </summary>
public class DynamicApiRouteOptions
{
    /// <summary>
    /// 是否使用命名空间作为路由段
    /// </summary>
    public bool UseNamespaceAsRoute { get; set; } = false;

    /// <summary>
    /// 要排除的命名空间前缀
    /// </summary>
    public List<string> NamespacePrefixesToExclude { get; set; } = [];

    /// <summary>
    /// 是否将模块名称作为路由段
    /// </summary>
    public bool UseModuleNameAsRoute { get; set; } = true;

    /// <summary>
    /// 模块名称提取正则表达式
    /// </summary>
    public string? ModuleNamePattern { get; set; }
}
