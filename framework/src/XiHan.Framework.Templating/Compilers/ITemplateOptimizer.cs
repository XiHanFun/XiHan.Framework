#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateOptimizer
// Guid:0h5j9j4f-8i1k-9g4h-5j0j-9f4i1k8h5j0j
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Parsers;

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板优化器接口
/// </summary>
public interface ITemplateOptimizer
{
    /// <summary>
    /// 优化模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">优化选项</param>
    /// <returns>优化结果</returns>
    TemplateOptimizationResult Optimize(string templateSource, TemplateOptimizationOptions? options = null);

    /// <summary>
    /// 异步优化模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">优化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>优化结果</returns>
    Task<TemplateOptimizationResult> OptimizeAsync(string templateSource, TemplateOptimizationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 优化模板AST
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">优化选项</param>
    /// <returns>优化后的AST</returns>
    ITemplateAst OptimizeAst(ITemplateAst ast, TemplateOptimizationOptions? options = null);

    /// <summary>
    /// 分析模板性能
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>性能分析报告</returns>
    TemplatePerformanceReport AnalyzePerformance(string templateSource);
}
