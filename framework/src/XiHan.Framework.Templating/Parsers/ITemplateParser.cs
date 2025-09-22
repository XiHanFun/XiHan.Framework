#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateParser
// Guid:4b9e3d8f-2c5e-4f8b-9e4d-3f8c5b9e2d8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
