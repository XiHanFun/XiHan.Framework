#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiAttribute
// Guid:963f4164-1fcd-460c-bc28-f33c7686f2fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Attributes;

/// <summary>
/// 动态 API 特性
/// 标记类或方法以配置动态 API 行为
/// 合并优先级：全局配置 &lt; 类级特性 &lt; 方法级特性
/// 同一层级按 Order 升序合并（后写入覆盖先写入）
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class DynamicApiAttribute : Attribute
{
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

    /// <summary>
    /// 是否启用动态 API【单值】
    /// 任一层级设置为 false 都会禁用对应 API
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 路由模板【单值】
    /// 方法级优先，类级兜底
    /// </summary>
    public string RouteTemplate { get; set; } = string.Empty;

    /// <summary>
    /// API 名称【单值】
    /// 方法级优先，类级兜底
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// API 版本【可叠加】
    /// 可通过多次标注叠加多个版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// API 描述【单值】
    /// 方法级优先，类级兜底
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// API 标签【可叠加】
    /// 可通过多次标注叠加多个标签
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// API 分组（文档键）【可叠加】
    /// 用于生成 OpenApi 文档路径/文档标识
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// API 分组显示名【单值】
    /// 用于文档 UI 展示名（如 Swagger/Scalar 的分组标题）
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 同层级合并顺序【单值】
    /// 数值越大优先级越高（后应用）
    /// </summary>
    public int Order { get; set; }

    private bool? _preserveRoutePredicate;

    private bool? _usePascalCaseRoutes;

    private bool? _useLowercaseRoute;

    /// <summary>
    /// 是否保留路由谓词（例如 Get/Create/Delete 前缀）【单值】
    /// 方法级优先，类级兜底，最终回退全局配置。
    /// 经可空后备字段区分"未设置"：只为 Group/Tag 标注特性时不会以编译期默认值
    /// 覆盖全局配置（该陷阱曾导致整类路由 404）。读取请用 <see cref="PreserveRoutePredicateOrNull"/>。
    /// </summary>
    public bool PreserveRoutePredicate
    {
        get => _preserveRoutePredicate ?? false;
        set => _preserveRoutePredicate = value;
    }

    /// <summary>
    /// 路由谓词设置原值（null=未显式设置，回退下一层级）
    /// </summary>
    public bool? PreserveRoutePredicateOrNull => _preserveRoutePredicate;

    /// <summary>
    /// 是否使用 PascalCase 路由【单值】
    /// 方法级优先，类级兜底，最终回退全局配置（未设置不覆盖）。读取请用 <see cref="UsePascalCaseRoutesOrNull"/>。
    /// </summary>
    public bool UsePascalCaseRoutes
    {
        get => _usePascalCaseRoutes ?? true;
        set => _usePascalCaseRoutes = value;
    }

    /// <summary>
    /// PascalCase 路由设置原值（null=未显式设置，回退下一层级）
    /// </summary>
    public bool? UsePascalCaseRoutesOrNull => _usePascalCaseRoutes;

    /// <summary>
    /// 是否使用小写路由【单值】
    /// 方法级优先，类级兜底，最终回退全局配置（未设置不覆盖）。读取请用 <see cref="UseLowercaseRouteOrNull"/>。
    /// </summary>
    public bool UseLowercaseRoute
    {
        get => _useLowercaseRoute ?? false;
        set => _useLowercaseRoute = value;
    }

    /// <summary>
    /// 小写路由设置原值（null=未显式设置，回退下一层级）
    /// </summary>
    public bool? UseLowercaseRouteOrNull => _useLowercaseRoute;

    /// <summary>
    /// 是否在 API 浏览器中显示【单值】
    /// 任一层级设置为 false 都会隐藏对应 API
    /// </summary>
    public bool VisibleInApiExplorer { get; set; } = true;

    /// <summary>
    /// 自定义属性【可叠加】
    /// 建议使用 key=value，重复 key 以后写入覆盖前写入
    /// </summary>
    public string CustomProperties { get; set; } = string.Empty;
}
