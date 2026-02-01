#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateBlockParser
// Guid:2924fc25-a368-42c3-b53f-ce993bbab880
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:11:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
