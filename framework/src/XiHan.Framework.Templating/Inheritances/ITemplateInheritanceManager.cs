#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateInheritanceManager
// Guid:7e2g6g1c-5f8h-6d1e-2g7g-6c1f8h5e2g7g
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板继承管理器接口
/// </summary>
public interface ITemplateInheritanceManager
{
    /// <summary>
    /// 注册布局模板
    /// </summary>
    /// <param name="name">布局名称</param>
    /// <param name="template">布局模板</param>
    void RegisterLayout(string name, string template);

    /// <summary>
    /// 获取布局模板
    /// </summary>
    /// <param name="name">布局名称</param>
    /// <returns>布局模板</returns>
    string? GetLayout(string name);

    /// <summary>
    /// 解析模板继承关系
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>继承信息</returns>
    TemplateInheritanceInfo ParseInheritance(string templateSource);

    /// <summary>
    /// 合并模板和布局
    /// </summary>
    /// <param name="template">子模板</param>
    /// <param name="layout">布局模板</param>
    /// <param name="blocks">块定义</param>
    /// <returns>合并后的模板</returns>
    string MergeTemplate(string template, string layout, IDictionary<string, string> blocks);

    /// <summary>
    /// 渲染继承模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderInheritedTemplateAsync(string templateSource, ITemplateContext context);
}
