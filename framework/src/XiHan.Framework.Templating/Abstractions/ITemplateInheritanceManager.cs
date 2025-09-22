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

namespace XiHan.Framework.Templating.Abstractions;

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

/// <summary>
/// 布局模板解析器
/// </summary>
public interface ILayoutTemplateResolver
{
    /// <summary>
    /// 解析布局模板
    /// </summary>
    /// <param name="layoutName">布局名称</param>
    /// <returns>布局模板</returns>
    Task<string?> ResolveLayoutAsync(string layoutName);

    /// <summary>
    /// 解析布局文件
    /// </summary>
    /// <param name="layoutPath">布局文件路径</param>
    /// <returns>布局模板</returns>
    Task<string?> ResolveLayoutFileAsync(string layoutPath);

    /// <summary>
    /// 缓存布局模板
    /// </summary>
    /// <param name="layoutName">布局名称</param>
    /// <param name="template">布局模板</param>
    void CacheLayout(string layoutName, string template);

    /// <summary>
    /// 清空布局缓存
    /// </summary>
    void ClearLayoutCache();
}

/// <summary>
/// 模板继承信息
/// </summary>
public record TemplateInheritanceInfo
{
    /// <summary>
    /// 是否有继承
    /// </summary>
    public bool HasInheritance { get; init; }

    /// <summary>
    /// 父布局名称
    /// </summary>
    public string? ParentLayout { get; init; }

    /// <summary>
    /// 块定义
    /// </summary>
    public IDictionary<string, TemplateBlock> Blocks { get; init; } = new Dictionary<string, TemplateBlock>();

    /// <summary>
    /// 内容区域
    /// </summary>
    public string? ContentArea { get; init; }

    /// <summary>
    /// 继承链
    /// </summary>
    public IList<string> InheritanceChain { get; init; } = [];
}

/// <summary>
/// 模板块
/// </summary>
public record TemplateBlock
{
    /// <summary>
    /// 块名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 块内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 是否可覆盖
    /// </summary>
    public bool IsOverridable { get; init; } = true;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; init; } = false;

    /// <summary>
    /// 默认内容
    /// </summary>
    public string? DefaultContent { get; init; }

    /// <summary>
    /// 块位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }
}

/// <summary>
/// 模板块解析器
/// </summary>
public interface ITemplateBlockParser
{
    /// <summary>
    /// 解析模板块
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析的块集合</returns>
    IDictionary<string, TemplateBlock> ParseBlocks(string templateSource);

    /// <summary>
    /// 提取块内容
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="blockName">块名称</param>
    /// <returns>块内容</returns>
    string? ExtractBlockContent(string templateSource, string blockName);

    /// <summary>
    /// 替换块内容
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="blockName">块名称</param>
    /// <param name="newContent">新内容</param>
    /// <returns>替换后的模板</returns>
    string ReplaceBlockContent(string templateSource, string blockName, string newContent);

    /// <summary>
    /// 验证块语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateBlocks(string templateSource);
}

/// <summary>
/// 模板继承验证器
/// </summary>
public interface ITemplateInheritanceValidator
{
    /// <summary>
    /// 验证继承语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateInheritance(string templateSource);

    /// <summary>
    /// 验证继承链
    /// </summary>
    /// <param name="inheritanceChain">继承链</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateInheritanceChain(IList<string> inheritanceChain);

    /// <summary>
    /// 检测循环继承
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="inheritanceChain">继承链</param>
    /// <returns>是否存在循环继承</returns>
    bool DetectCircularInheritance(string templateName, IList<string> inheritanceChain);
}
