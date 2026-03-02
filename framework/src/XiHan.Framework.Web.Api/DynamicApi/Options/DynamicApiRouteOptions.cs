#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiRouteOptions
// Guid:166ccce1-f82e-486c-9713-3d6e43dd23fe
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Options;

/// <summary>
/// 动态 API 路由配置
/// </summary>
public class DynamicApiRouteOptions
{
    /// <summary>
    /// 是否使用命名空间作为路由段
    /// true: 命名空间将作为路由的一部分
    /// false: 命名空间不会作为路由的一部分
    /// </summary>
    public bool UseNamespaceAsRoute { get; set; } = false;

    /// <summary>
    /// 要排除的命名空间前缀
    /// 例如: XiHan.Framework.Web.Api.Controllers -> XiHan.Framework.Web.Api
    /// </summary>
    public List<string> NamespacePrefixesToExclude { get; set; } = [];

    /// <summary>
    /// 是否将模块名称作为路由段
    /// true: 模块名称将作为路由的一部分
    /// false: 模块名称不会作为路由的一部分
    /// </summary>
    public bool UseModuleNameAsRoute { get; set; } = false;

    /// <summary>
    /// 模块名称提取正则表达式
    /// 用于从命名空间中提取模块名称
    /// </summary>
    public string? ModuleNamePattern { get; set; }
}
