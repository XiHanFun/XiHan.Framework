// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Inheritances;

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
