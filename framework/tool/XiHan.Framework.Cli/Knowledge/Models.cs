// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 文档站的一页
/// </summary>
/// <param name="Path">站内路径，不带扩展名，如 guide/caching、packages/caching</param>
/// <param name="Title">一级标题</param>
/// <param name="Summary">一句话摘要</param>
/// <param name="Markdown">正文，已去掉 frontmatter</param>
internal sealed record DocPage(string Path, string Title, string Summary, string Markdown);

/// <summary>
/// 模块类及其显式依赖
/// </summary>
/// <param name="Name">模块类名</param>
/// <param name="DependsOn">[DependsOn] 声明的模块类名</param>
internal sealed record ModuleInfo(string Name, IReadOnlyList<string> DependsOn);

/// <summary>
/// 第三方 NuGet 依赖
/// </summary>
/// <param name="Name">包名</param>
/// <param name="Version">版本</param>
internal sealed record PackageDependency(string Name, string Version);

/// <summary>
/// 一个框架包
/// </summary>
internal sealed record PackageInfo
{
    /// <summary>
    /// 包名，如 XiHan.Framework.Caching
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 项目文件里的包说明
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 文档页给出的所在层
    /// </summary>
    public string? Layer { get; init; }

    /// <summary>
    /// 文档页开头的一句话定位
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 对应的文档页路径
    /// </summary>
    public string? DocPath { get; init; }

    /// <summary>
    /// 包内的模块类
    /// </summary>
    public required IReadOnlyList<ModuleInfo> Modules { get; init; }

    /// <summary>
    /// 引用的其他框架包
    /// </summary>
    public required IReadOnlyList<string> FrameworkDependencies { get; init; }

    /// <summary>
    /// 引用的第三方包
    /// </summary>
    public required IReadOnlyList<PackageDependency> ThirdPartyDependencies { get; init; }
}

/// <summary>
/// 选项类的一个属性
/// </summary>
/// <param name="Name">属性名</param>
/// <param name="Type">类型</param>
/// <param name="Default">初始化表达式，没有时为 null</param>
/// <param name="Summary">XML 文档摘要</param>
internal sealed record OptionProperty(string Name, string Type, string? Default, string Summary);

/// <summary>
/// 选项文件里声明的一个类型
/// </summary>
/// <param name="Name">类型名</param>
/// <param name="Summary">XML 文档摘要</param>
/// <param name="Properties">公开属性</param>
internal sealed record OptionType(string Name, string Summary, IReadOnlyList<OptionProperty> Properties);

/// <summary>
/// 一个配置节
/// </summary>
internal sealed record ConfigSection
{
    /// <summary>
    /// 配置节路径，如 XiHan:Caching:Redis
    /// </summary>
    public required string Section { get; init; }

    /// <summary>
    /// 所属包
    /// </summary>
    public required string Package { get; init; }

    /// <summary>
    /// 绑定的选项类
    /// </summary>
    public required OptionType Options { get; init; }

    /// <summary>
    /// 同一文件里被属性引用的其他类型
    /// </summary>
    public required IReadOnlyList<OptionType> NestedTypes { get; init; }
}

/// <summary>
/// 更新日志的一个版本
/// </summary>
/// <param name="Version">版本号，不带 v</param>
/// <param name="Date">发布日期</param>
/// <param name="Body">正文</param>
internal sealed record ChangelogSection(string Version, string Date, string Body);
