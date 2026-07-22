// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Parsers;

/// <summary>
/// 模板解析器接口
/// </summary>
public interface ITemplateParser
{
    /// <summary>
    /// 解析模板源码
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>模板抽象语法树</returns>
    ITemplateAst Parse(string templateSource);

    /// <summary>
    /// 解析模板文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模板抽象语法树</returns>
    Task<ITemplateAst> ParseFileAsync(string templateFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析模板表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>表达式节点</returns>
    ITemplateExpression ParseExpression(string expression);
}
